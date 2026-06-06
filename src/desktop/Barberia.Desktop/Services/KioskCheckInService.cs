using System.Text.Json;
using Barberia.Core.Assignment;
using Barberia.Core.Domain;
using Barberia.Data;
using Barberia.Data.Models;
using Barberia.Data.Repositories;
using Barberia.Hardware.Pos;

namespace Barberia.Desktop.Services;

public sealed class KioskCheckInService
{
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly IKioskTicketPrinter _ticketPrinter;
    private readonly TurnAssignmentEngine _assignmentEngine = new();

    public KioskCheckInService()
        : this(LocalDesktopDatabase.CreateConnectionFactory(), new SimulatedKioskTicketPrinter())
    {
    }

    public KioskCheckInService(SqliteConnectionFactory connectionFactory, IKioskTicketPrinter ticketPrinter)
    {
        _connectionFactory = connectionFactory;
        _ticketPrinter = ticketPrinter;
        new LocalDatabaseInitializer(_connectionFactory).Initialize();
    }

    public KioskCheckInSnapshot Load()
    {
        using var connection = _connectionFactory.OpenConnection();
        var barbers = new LocalBarberRepository(connection)
            .ListAll()
            .Where(barber => barber.IsActive)
            .ToArray();

        return new KioskCheckInSnapshot(DateTimeOffset.Now, barbers);
    }

    public KioskCheckInResult RegisterWalkIn(
        string customerName,
        bool acceptsAnyBarber,
        IReadOnlyCollection<Guid> requestedBarberIds)
    {
        var normalizedCustomerName = NormalizeCustomerName(customerName);
        var requestedIds = NormalizeRequestedBarbers(acceptsAnyBarber, requestedBarberIds);
        var now = DateTimeOffset.Now;
        var deviceId = Environment.MachineName;
        var turn = new Turn(
            Guid.NewGuid(),
            CreateTicketNumber(now),
            TurnState.Waiting,
            TurnSource.WalkIn,
            now,
            requestedBarberIds: acceptsAnyBarber ? null : requestedIds,
            customerName: normalizedCustomerName);

        KioskCheckInResult? result = null;
        var transaction = new LocalDataTransaction(_connectionFactory);
        transaction.Execute((connection, sqliteTransaction) =>
        {
            var turnRepository = new LocalTurnRepository(connection, sqliteTransaction);
            var barberRepository = new LocalBarberRepository(connection, sqliteTransaction);
            var appointmentRepository = new AppointmentReservationRepository(connection, sqliteTransaction);
            var auditRepository = new AuditEventRepository(connection, sqliteTransaction);

            var barbers = barberRepository
                .ListAll()
                .Where(barber => barber.IsActive)
                .ToArray();
            var requestedBarbers = ResolveRequestedBarbers(acceptsAnyBarber, requestedIds, barbers);
            turnRepository.Upsert(turn, now);

            var waitingTurns = turnRepository.ListWaiting();
            var rotationQueue = barbers
                .OrderBy(barber => barber.RotationOrder)
                .Select(barber => barber.Id)
                .ToArray();
            var appointments = appointmentRepository.ListBetween(now.AddMinutes(-1), now.AddMinutes(15));
            string? assignedBarberName = null;
            string? assignedBarberStationCode = null;
            var status = KioskCheckInStatus.Waiting;
            var message = acceptsAnyBarber
                ? "Ticket printed. We will call you when a barber is ready."
                : "Ticket printed. We will call you when one of your selected barbers is ready.";

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
                result = CreatePrintedResult(
                    turn,
                    requestedBarbers,
                    acceptsAnyBarber,
                    now,
                    assignedBarberName,
                    assignedBarberStationCode,
                    status,
                    message,
                    deviceId,
                    auditRepository);
                return;
            }

            turnRepository.ApplyAssignment(decision.TurnId, decision.BarberId, decision.TurnState, now);
            barberRepository.ApplyAssignment(decision.BarberId, decision.BarberState, now);

            if (decision.TurnId == turn.Id)
            {
                var assignedBarber = barbers.First(barber => barber.Id == decision.BarberId);
                assignedBarberName = assignedBarber.DisplayName;
                assignedBarberStationCode = assignedBarber.StationCode;
                status = KioskCheckInStatus.Assigned;
                message = "Ticket printed. Your barber has been notified.";
            }

            result = CreatePrintedResult(
                turn,
                requestedBarbers,
                acceptsAnyBarber,
                now,
                assignedBarberName,
                assignedBarberStationCode,
                status,
                message,
                deviceId,
                auditRepository);
        });

        return result ?? throw new InvalidOperationException("Kiosk check-in did not produce a ticket.");
    }

    private KioskCheckInResult CreatePrintedResult(
        Turn turn,
        IReadOnlyList<Barber> requestedBarbers,
        bool acceptsAnyBarber,
        DateTimeOffset checkedInAt,
        string? assignedBarberName,
        string? assignedBarberStationCode,
        KioskCheckInStatus status,
        string message,
        string deviceId,
        AuditEventRepository auditRepository)
    {
        var requestedBarberNames = requestedBarbers
            .Select(barber => barber.DisplayName)
            .ToArray();
        var requestedBarberStationCodes = requestedBarbers
            .Select(barber => barber.StationCode)
            .ToArray();
        var printResult = _ticketPrinter.Print(new KioskTicketPrintJob(
            turn.TicketNumber,
            CreateTicketQrPayload(turn),
            turn.CustomerName ?? throw new InvalidOperationException("Customer name is required for kiosk ticket printing."),
            requestedBarberNames,
            requestedBarberStationCodes,
            acceptsAnyBarber,
            assignedBarberName,
            assignedBarberStationCode,
            checkedInAt,
            deviceId));

        if (!printResult.Succeeded)
        {
            throw new InvalidOperationException($"Could not print the ticket: {printResult.ErrorMessage}");
        }

        auditRepository.Add(new AuditEvent(
            Guid.NewGuid(),
            checkedInAt,
            "turn_checked_in",
            "turn",
            turn.Id,
            JsonSerializer.Serialize(new
            {
                ticket = turn.TicketNumber,
                qrPayload = CreateTicketQrPayload(turn),
                customerName = turn.CustomerName,
                acceptsAnyBarber,
                requestedBarberIds = turn.RequestedBarberIds,
                requestedBarberNames,
                requestedBarberStationCodes,
                assignedBarberName,
                assignedBarberStationCode,
                status = status.ToString(),
                ticketPrinted = true
            }),
            deviceId));

        return new KioskCheckInResult(
            turn.TicketNumber,
            turn.CustomerName,
            checkedInAt,
            assignedBarberName,
            assignedBarberStationCode,
            requestedBarberNames,
            requestedBarberStationCodes,
            acceptsAnyBarber,
            status,
            message);
    }

    private static string NormalizeCustomerName(string customerName)
    {
        if (string.IsNullOrWhiteSpace(customerName))
        {
            throw new InvalidOperationException("Enter your name to continue.");
        }

        return customerName.Trim();
    }

    private static IReadOnlyList<Guid> NormalizeRequestedBarbers(
        bool acceptsAnyBarber,
        IReadOnlyCollection<Guid>? requestedBarberIds)
    {
        var requestedIds = requestedBarberIds?
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray() ?? [];

        if (!acceptsAnyBarber && requestedIds.Length == 0)
        {
            throw new InvalidOperationException("Choose Any Barber or at least one barber before printing.");
        }

        return requestedIds;
    }

    private static IReadOnlyList<Barber> ResolveRequestedBarbers(
        bool acceptsAnyBarber,
        IReadOnlyList<Guid> requestedIds,
        IReadOnlyList<Barber> barbers)
    {
        if (acceptsAnyBarber)
        {
            return [];
        }

        var requestedSet = requestedIds.ToHashSet();
        var requestedBarbers = barbers
            .Where(barber => requestedSet.Contains(barber.Id))
            .ToArray();

        if (requestedBarbers.Length != requestedSet.Count)
        {
            throw new InvalidOperationException("One or more selected barbers were not found locally.");
        }

        return requestedBarbers;
    }

    private static string CreateTicketNumber(DateTimeOffset timestamp)
    {
        return $"W{timestamp:yyyyMMddHHmmssfff}";
    }

    private static string CreateTicketQrPayload(Turn turn)
    {
        return turn.TicketNumber;
    }
}
