using Barberia.Core.Domain;
using Barberia.Data;
using Barberia.Data.Repositories;

namespace Barberia.Desktop.Services;

public sealed class BarberPanelService
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public BarberPanelService()
        : this(LocalDesktopDatabase.CreateConnectionFactory())
    {
    }

    public BarberPanelService(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
        new LocalDatabaseInitializer(_connectionFactory).Initialize();
    }

    public BarberPanelSnapshot Load(Guid? selectedBarberId)
    {
        var now = OperationalClock.Now;
        DailyOperationCoordinator.EnsureDailyReset(_connectionFactory, now, Environment.MachineName);
        var businessDate = DailyOperationCoordinator.GetBusinessDate(now);

        using var connection = _connectionFactory.OpenConnection();
        var barberRepository = new LocalBarberRepository(connection);
        var turnRepository = new LocalTurnRepository(connection);
        var dailyRotationEntries = new DailyRotationRepository(connection).ListByDate(businessDate);
        var barbers = DailyRotationQueue.OrderBarbers(
            barberRepository
            .ListAll()
            .Where(barber => barber.IsActive),
            dailyRotationEntries,
            businessDate);
        var assignedTurns = selectedBarberId is null
            ? turnRepository.ListAssignedCalled()
            : turnRepository.ListAssignedToBarber(selectedBarberId.Value);

        return new BarberPanelSnapshot(now, barbers, assignedTurns);
    }

    public void MarkAvailable(Guid barberId)
    {
        UpdateOperationalState(barberId, BarberState.Available);
    }

    public void MarkOffline(Guid barberId)
    {
        UpdateOperationalState(barberId, BarberState.Offline);
    }

    public BarberPanelStartResult StartService(string scannedTicketNumber)
    {
        if (string.IsNullOrWhiteSpace(scannedTicketNumber))
        {
            throw new InvalidOperationException("Scan or enter an assigned ticket.");
        }

        var now = OperationalClock.Now;
        BarberPanelStartResult? result = null;

        var transaction = new LocalDataTransaction(_connectionFactory);
        transaction.Execute((connection, sqliteTransaction) =>
        {
            DailyOperationCoordinator.EnsureDailyReset(connection, sqliteTransaction, now, Environment.MachineName);

            var barberRepository = new LocalBarberRepository(connection, sqliteTransaction);
            var turnRepository = new LocalTurnRepository(connection, sqliteTransaction);

            var turn = turnRepository.GetByTicketInputForToday(scannedTicketNumber, now)
                ?? throw new InvalidOperationException("Ticket does not exist");

            var barberId = turn.AssignedBarberId
                ?? throw new InvalidOperationException("The ticket does not have an assigned barber.");
            var barber = barberRepository.GetById(barberId)
                ?? throw new InvalidOperationException("The assigned barber does not exist in the local database.");

            if (turn.State == TurnState.InService)
            {
                throw new InvalidOperationException("This ticket is already being attended by another barber.");
            }

            if (turn.State == TurnState.Completed)
            {
                throw new InvalidOperationException($"This ticket was already completed and charged by {barber.DisplayName}.");
            }
            if (!barber.IsActive)
            {
                throw new InvalidOperationException("The assigned barber is disabled by administration.");
            }

            if (turn.State is not TurnState.Called)
            {
                throw new InvalidOperationException("The ticket is not ready to start service.");
            }

            if (barber.State != BarberState.Called)
            {
                throw new InvalidOperationException("The barber must have a called ticket before starting service.");
            }

            turnRepository.MarkInService(turn.Id, barberId, now);
            barberRepository.SetState(barberId, BarberState.InService, now);

            result = new BarberPanelStartResult(
                barber.Id,
                turn.DisplayTicketNumber,
                turn.TicketNumber,
                barber.DisplayName,
                barber.StationCode ?? throw new InvalidOperationException("The active barber does not have an assigned station."),
                now,
                "Service started locally. Payment and closeout remain in Cash Box.");
        });

        return result ?? throw new InvalidOperationException("Service could not be started.");
    }

    private void UpdateOperationalState(Guid barberId, BarberState state)
    {
        var now = OperationalClock.Now;
        var deviceId = Environment.MachineName;
        var transaction = new LocalDataTransaction(_connectionFactory);
        transaction.Execute((connection, sqliteTransaction) =>
        {
            DailyOperationCoordinator.EnsureDailyReset(connection, sqliteTransaction, now, deviceId);

            var barberRepository = new LocalBarberRepository(connection, sqliteTransaction);
            var barber = barberRepository.GetById(barberId)
                ?? throw new InvalidOperationException("Barber was not found in the local database.");
            if (!barber.IsActive)
            {
                throw new InvalidOperationException("This barber is disabled by administration.");
            }

            if (barber.State is BarberState.Called or BarberState.InService)
            {
                throw new InvalidOperationException("Availability cannot be changed while a ticket is called or in service.");
            }

            if (state == BarberState.Available)
            {
                var businessDate = DailyOperationCoordinator.GetBusinessDate(now);
                var checkedInAt = barber.CheckedInAt is DateTimeOffset existingCheckedInAt
                    && OperationalClock.GetBusinessDate(existingCheckedInAt) == businessDate
                        ? existingCheckedInAt
                        : now;

                barberRepository.SetStateAndCheckedInAt(barberId, state, checkedInAt, now);
                new DailyRotationRepository(connection, sqliteTransaction)
                    .EnsureQueued(businessDate, barberId, checkedInAt, now);
            }
            else
            {
                barberRepository.SetState(barberId, state, now);
            }
        });
    }
}
