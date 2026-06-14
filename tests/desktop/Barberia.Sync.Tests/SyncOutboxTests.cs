using Barberia.ApiClient.Sync;
using Barberia.Data;
using Barberia.Data.Sync;
using Barberia.Sync.Outbox;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Barberia.Sync.Tests;

public sealed class SyncOutboxTests
{
    [Fact]
    public void Recorder_StoresEventInLocalOutboxWithoutCallingCloud()
    {
        using var database = TestDatabase.Create();
        var eventId = Guid.NewGuid();
        var turnId = Guid.NewGuid();
        var occurredAt = DateTimeOffset.Parse("2026-06-04T13:00:00Z");
        var createdAt = DateTimeOffset.Parse("2026-06-04T13:00:01Z");

        var queued = new SyncOutboxRecorder(database.OutboxStore).Enqueue(
            new LocalSyncEvent(
                eventId,
                occurredAt,
                "turn.checked_in",
                "turn",
                turnId,
                """{"ticketNumber":"A-001"}""",
                "kiosk-1"),
            createdAt);

        var saved = new SyncOutboxRepository(database.Connection).GetById(eventId);

        Assert.NotNull(saved);
        Assert.Equal(queued.Id, saved.Id);
        Assert.Equal(SyncOutboxEventState.Pending, saved.State);
        Assert.Equal(0, saved.AttemptCount);
        Assert.Equal(createdAt, saved.NextAttemptAt);
        Assert.Equal("turn.checked_in", saved.EventType);
        Assert.Equal("""{"ticketNumber":"A-001"}""", saved.Payload);
    }

    [Fact]
    public void Dispatcher_MarksReadyEventSyncedAfterSuccessfulPush()
    {
        using var database = TestDatabase.Create();
        var eventId = Guid.NewGuid();
        var turnId = Guid.NewGuid();
        var now = DateTimeOffset.Parse("2026-06-04T14:00:00Z");
        new SyncOutboxRecorder(database.OutboxStore).Enqueue(
            new LocalSyncEvent(eventId, now.AddMinutes(-1), "turn.assigned", "turn", turnId, "{}", "kiosk-1"),
            now.AddMinutes(-1));
        var cloudClient = new FakeCloudSyncClient(CloudSyncResult.Success());

        var result = new SyncOutboxDispatcher(database.OutboxStore, cloudClient)
            .DispatchDueAsync(now).GetAwaiter().GetResult();

        var saved = new SyncOutboxRepository(database.Connection).GetById(eventId);
        Assert.Equal(new SyncDispatchResult(1, 1, 0), result);
        Assert.Single(cloudClient.Pushed);
        Assert.Equal(eventId, cloudClient.Pushed[0].Id);
        Assert.Equal(SyncOutboxEventState.Synced, saved?.State);
        Assert.Equal(now, saved?.SyncedAt);
        Assert.Null(saved?.LastError);
    }

    [Fact]
    public void Dispatcher_SchedulesRetryWhenCloudFailsAndSkipsUntilDue()
    {
        using var database = TestDatabase.Create();
        var eventId = Guid.NewGuid();
        var turnId = Guid.NewGuid();
        var now = DateTimeOffset.Parse("2026-06-04T15:00:00Z");
        var retryPolicy = new SyncRetryPolicy(TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(10));
        new SyncOutboxRecorder(database.OutboxStore).Enqueue(
            new LocalSyncEvent(eventId, now.AddMinutes(-1), "cash_box.closed", "turn", turnId, "{}", "autocaja-1"),
            now.AddMinutes(-1));
        var cloudClient = new FakeCloudSyncClient(CloudSyncResult.Failure("offline"));
        var dispatcher = new SyncOutboxDispatcher(database.OutboxStore, cloudClient, retryPolicy);

        var failedResult = dispatcher.DispatchDueAsync(now).GetAwaiter().GetResult();
        var skippedResult = dispatcher.DispatchDueAsync(now.AddMinutes(1)).GetAwaiter().GetResult();

        var saved = new SyncOutboxRepository(database.Connection).GetById(eventId);
        Assert.Equal(new SyncDispatchResult(1, 0, 1), failedResult);
        Assert.Equal(new SyncDispatchResult(0, 0, 0), skippedResult);
        Assert.Single(cloudClient.Pushed);
        Assert.Equal(SyncOutboxEventState.Pending, saved?.State);
        Assert.Equal(1, saved?.AttemptCount);
        Assert.Equal(now.AddMinutes(2), saved?.NextAttemptAt);
        Assert.Equal("offline", saved?.LastError);
    }

    [Fact]
    public void Dispatcher_ConvertsApiExceptionsIntoRetriesWithoutThrowing()
    {
        using var database = TestDatabase.Create();
        var eventId = Guid.NewGuid();
        var turnId = Guid.NewGuid();
        var now = DateTimeOffset.Parse("2026-06-04T16:00:00Z");
        new SyncOutboxRecorder(database.OutboxStore).Enqueue(
            new LocalSyncEvent(eventId, now.AddMinutes(-1), "barber.state_changed", "barber", turnId, "{}", "panel-1"),
            now.AddMinutes(-1));
        var cloudClient = new FakeCloudSyncClient(new InvalidOperationException("API unavailable"));

        var result = new SyncOutboxDispatcher(database.OutboxStore, cloudClient)
            .DispatchDueAsync(now).GetAwaiter().GetResult();

        var saved = new SyncOutboxRepository(database.Connection).GetById(eventId);
        Assert.Equal(new SyncDispatchResult(1, 0, 1), result);
        Assert.Equal(SyncOutboxEventState.Pending, saved?.State);
        Assert.Equal(1, saved?.AttemptCount);
        Assert.Equal(now.AddMinutes(1), saved?.NextAttemptAt);
        Assert.Equal("API unavailable", saved?.LastError);
    }

    [Fact]
    public void Dispatcher_ContinuesProcessingLaterEventsAfterFailure()
    {
        using var database = TestDatabase.Create();
        var firstEventId = Guid.NewGuid();
        var secondEventId = Guid.NewGuid();
        var now = DateTimeOffset.Parse("2026-06-04T17:00:00Z");
        var recorder = new SyncOutboxRecorder(database.OutboxStore);
        recorder.Enqueue(
            new LocalSyncEvent(firstEventId, now.AddMinutes(-2), "turn.checked_in", "turn", Guid.NewGuid(), "{}", "kiosk-1"),
            now.AddMinutes(-2));
        recorder.Enqueue(
            new LocalSyncEvent(secondEventId, now.AddMinutes(-1), "turn.assigned", "turn", Guid.NewGuid(), "{}", "kiosk-1"),
            now.AddMinutes(-1));
        var cloudClient = new FakeCloudSyncClient(
            CloudSyncResult.Failure("temporary outage"),
            CloudSyncResult.Success());

        var result = new SyncOutboxDispatcher(database.OutboxStore, cloudClient)
            .DispatchDueAsync(now).GetAwaiter().GetResult();

        var repository = new SyncOutboxRepository(database.Connection);
        Assert.Equal(new SyncDispatchResult(2, 1, 1), result);
        Assert.Equal(SyncOutboxEventState.Pending, repository.GetById(firstEventId)?.State);
        Assert.Equal(SyncOutboxEventState.Synced, repository.GetById(secondEventId)?.State);
    }

    private sealed class FakeCloudSyncClient : ICloudSyncClient
    {
        private readonly Queue<object> _responses;

        public FakeCloudSyncClient(params object[] responses)
        {
            _responses = new Queue<object>(responses);
        }

        public List<CloudSyncEnvelope> Pushed { get; } = [];

        public Task<CloudSyncResult> PushAsync(CloudSyncEnvelope envelope)
        {
            Pushed.Add(envelope);

            if (_responses.Count == 0)
            {
                return Task.FromResult(CloudSyncResult.Success());
            }

            var response = _responses.Dequeue();
            if (response is Exception exception)
            {
                throw exception;
            }

            return Task.FromResult((CloudSyncResult)response);
        }
    }

    private sealed class TestDatabase : IDisposable
    {
        private readonly SqliteConnection _keepAliveConnection;

        private TestDatabase(SqliteConnectionFactory connectionFactory, SqliteConnection keepAliveConnection)
        {
            ConnectionFactory = connectionFactory;
            _keepAliveConnection = keepAliveConnection;
            Connection = connectionFactory.OpenConnection();
        }

        public SqliteConnectionFactory ConnectionFactory { get; }

        public LocalSyncOutboxStore OutboxStore => new(ConnectionFactory);

        public SqliteConnection Connection { get; }

        public static TestDatabase Create()
        {
            var name = Guid.NewGuid().ToString("N");
            var connectionString = $"Data Source={name};Mode=Memory;Cache=Shared";
            var connectionFactory = new SqliteConnectionFactory(connectionString);
            var keepAliveConnection = connectionFactory.OpenConnection();
            LocalDatabaseInitializer.Initialize(keepAliveConnection);

            return new TestDatabase(connectionFactory, keepAliveConnection);
        }

        public void Dispose()
        {
            Connection.Dispose();
            _keepAliveConnection.Dispose();
        }
    }
}
