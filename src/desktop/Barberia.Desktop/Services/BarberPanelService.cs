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
        using var connection = _connectionFactory.OpenConnection();
        var barberRepository = new LocalBarberRepository(connection);
        var turnRepository = new LocalTurnRepository(connection);
        var barbers = barberRepository
            .ListAll()
            .Where(barber => barber.IsActive)
            .ToArray();
        var assignedTurns = selectedBarberId is null
            ? turnRepository.ListAssignedCalled()
            : turnRepository.ListAssignedToBarber(selectedBarberId.Value);

        return new BarberPanelSnapshot(DateTimeOffset.Now, barbers, assignedTurns);
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

        var now = DateTimeOffset.Now;
        BarberPanelStartResult? result = null;

        var transaction = new LocalDataTransaction(_connectionFactory);
        transaction.Execute((connection, sqliteTransaction) =>
        {
            var barberRepository = new LocalBarberRepository(connection, sqliteTransaction);
            var turnRepository = new LocalTurnRepository(connection, sqliteTransaction);

            var turn = turnRepository.GetByTicketInputForToday(scannedTicketNumber, now)
                ?? throw new InvalidOperationException("Ticket was not found in the local database.");

            var barberId = turn.AssignedBarberId
                ?? throw new InvalidOperationException("The ticket does not have an assigned barber.");
            var barber = barberRepository.GetById(barberId)
                ?? throw new InvalidOperationException("The assigned barber does not exist in the local database.");
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
        using var connection = _connectionFactory.OpenConnection();
        var barberRepository = new LocalBarberRepository(connection);
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

        barberRepository.SetState(barberId, state, DateTimeOffset.Now);
    }
}
