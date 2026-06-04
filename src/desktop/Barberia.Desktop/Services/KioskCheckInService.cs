using Barberia.Core.Assignment;
using Barberia.Core.Domain;
using Barberia.Data;
using Barberia.Data.Repositories;

namespace Barberia.Desktop.Services;

public sealed class KioskCheckInService
{
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly TurnAssignmentEngine _assignmentEngine = new();

    public KioskCheckInService()
        : this(CreateDefaultConnectionFactory())
    {
    }

    public KioskCheckInService(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
        new LocalDatabaseInitializer(_connectionFactory).Initialize();
    }

    public KioskCheckInResult RegisterWalkIn()
    {
        var now = DateTimeOffset.Now;
        var turn = new Turn(
            Guid.NewGuid(),
            CreateTicketNumber(now),
            TurnState.Waiting,
            TurnSource.WalkIn,
            now);

        KioskCheckInResult? result = null;
        var transaction = new LocalDataTransaction(_connectionFactory);
        transaction.Execute((connection, sqliteTransaction) =>
        {
            var turnRepository = new LocalTurnRepository(connection, sqliteTransaction);
            var barberRepository = new LocalBarberRepository(connection, sqliteTransaction);
            var appointmentRepository = new AppointmentReservationRepository(connection, sqliteTransaction);

            turnRepository.Upsert(turn, now);

            var waitingTurns = turnRepository.ListWaiting();
            var barbers = barberRepository.ListAll();
            var rotationQueue = barbers
                .OrderBy(barber => barber.RotationOrder)
                .Select(barber => barber.Id)
                .ToArray();
            var appointments = appointmentRepository.ListBetween(now.AddMinutes(-1), now.AddMinutes(15));

            TurnAssignmentDecision decision;
            try
            {
                decision = _assignmentEngine.AssignNextTurn(new TurnAssignmentRequest(
                    waitingTurns,
                    barbers,
                    rotationQueue,
                    now,
                    appointments));
            }
            catch (InvalidOperationException)
            {
                result = new KioskCheckInResult(
                    turn.TicketNumber,
                    now,
                    null,
                    KioskCheckInStatus.Waiting,
                    "Turno registrado en espera. Se asignara cuando haya un barbero disponible.");
                return;
            }

            turnRepository.ApplyAssignment(decision.TurnId, decision.BarberId, decision.TurnState, now);
            barberRepository.ApplyAssignment(decision.BarberId, decision.BarberState, now);

            if (decision.TurnId == turn.Id)
            {
                var assignedBarber = barbers.First(barber => barber.Id == decision.BarberId);
                result = new KioskCheckInResult(
                    turn.TicketNumber,
                    now,
                    assignedBarber.DisplayName,
                    KioskCheckInStatus.Assigned,
                    "Turno registrado y asignado localmente.");
            }
            else
            {
                result = new KioskCheckInResult(
                    turn.TicketNumber,
                    now,
                    null,
                    KioskCheckInStatus.Waiting,
                    "Turno registrado en espera. Hay turnos previos en la cola local.");
            }
        });

        return result ?? throw new InvalidOperationException("Walk-in check-in did not produce a result.");
    }

    private static SqliteConnectionFactory CreateDefaultConnectionFactory()
    {
        var dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BarberiaSystem");
        Directory.CreateDirectory(dataDirectory);

        var databasePath = Path.Combine(dataDirectory, "barberia-local.db");
        return new SqliteConnectionFactory($"Data Source={databasePath}");
    }

    private static string CreateTicketNumber(DateTimeOffset timestamp)
    {
        return $"W{timestamp:yyyyMMddHHmmssfff}";
    }
}
