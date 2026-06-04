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
        var barbers = barberRepository.ListAll();
        var assignedTurns = selectedBarberId is null
            ? []
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

    public BarberPanelStartResult StartService(Guid barberId, string scannedTicketNumber)
    {
        if (string.IsNullOrWhiteSpace(scannedTicketNumber))
        {
            throw new InvalidOperationException("Escanea o introduce un ticket asignado.");
        }

        var now = DateTimeOffset.Now;
        BarberPanelStartResult? result = null;

        var transaction = new LocalDataTransaction(_connectionFactory);
        transaction.Execute((connection, sqliteTransaction) =>
        {
            var barberRepository = new LocalBarberRepository(connection, sqliteTransaction);
            var turnRepository = new LocalTurnRepository(connection, sqliteTransaction);
            var barber = barberRepository.GetById(barberId)
                ?? throw new InvalidOperationException("Barbero no encontrado en la base local.");
            var turn = turnRepository.GetByTicketNumber(scannedTicketNumber)
                ?? throw new InvalidOperationException("Ticket no encontrado en la base local.");

            if (turn.AssignedBarberId != barberId)
            {
                throw new InvalidOperationException("El ticket no esta asignado al barbero seleccionado.");
            }

            if (turn.State is not (TurnState.Assigned or TurnState.Called))
            {
                throw new InvalidOperationException("El ticket no esta listo para iniciar atencion.");
            }

            if (barber.State != BarberState.Called)
            {
                throw new InvalidOperationException("El barbero debe tener un turno llamado antes de iniciar atencion.");
            }

            turnRepository.MarkInService(turn.Id, barberId, now);
            barberRepository.SetState(barberId, BarberState.InService, now);

            result = new BarberPanelStartResult(
                turn.TicketNumber,
                barber.DisplayName,
                now,
                "Atencion iniciada localmente. El cierre operativo queda en autocaja.");
        });

        return result ?? throw new InvalidOperationException("No se pudo iniciar la atencion.");
    }

    private void UpdateOperationalState(Guid barberId, BarberState state)
    {
        using var connection = _connectionFactory.OpenConnection();
        var barberRepository = new LocalBarberRepository(connection);
        var barber = barberRepository.GetById(barberId)
            ?? throw new InvalidOperationException("Barbero no encontrado en la base local.");

        if (barber.State is BarberState.Called or BarberState.InService)
        {
            throw new InvalidOperationException("No se puede cambiar disponibilidad con un turno llamado o en servicio.");
        }

        barberRepository.SetState(barberId, state, DateTimeOffset.Now);
    }
}
