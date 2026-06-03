using Barberia.Core.Domain;
using Xunit;

namespace Barberia.Core.Tests;

public sealed class DomainModelTests
{
    [Fact]
    public void BarberStates_MatchConfirmedInitialStates()
    {
        BarberState[] states =
        [
            BarberState.NotCheckedIn,
            BarberState.Available,
            BarberState.Called,
            BarberState.InService,
            BarberState.Offline,
        ];

        Assert.Equal(5, states.Distinct().Count());
    }

    [Fact]
    public void TurnStates_MatchConfirmedInitialStates()
    {
        TurnState[] states =
        [
            TurnState.Waiting,
            TurnState.Assigned,
            TurnState.Called,
            TurnState.InService,
            TurnState.Completed,
            TurnState.Cancelled,
            TurnState.NoShow,
            TurnState.Voided,
        ];

        Assert.Equal(8, states.Distinct().Count());
    }

    [Fact]
    public void Turn_NormalizesRequestedBarbersWithoutDuplicatingIds()
    {
        var barberId = Guid.NewGuid();

        var turn = new Turn(
            Guid.NewGuid(),
            " A-001 ",
            TurnState.Waiting,
            TurnSource.WalkIn,
            DateTimeOffset.UtcNow,
            requestedBarberIds: [barberId, barberId]);

        Assert.Equal("A-001", turn.TicketNumber);
        Assert.Equal([barberId], turn.RequestedBarberIds);
    }

    [Fact]
    public void AppointmentReservation_UsesConfirmedProtectionWindow()
    {
        var appointment = new AppointmentReservation(
            Guid.NewGuid(),
            Guid.NewGuid(),
            AppointmentState.Confirmed,
            DateTimeOffset.UtcNow.AddHours(1),
            AppointmentReservation.DefaultProtectionWindow);

        Assert.Equal(TimeSpan.FromMinutes(15), appointment.ProtectionWindow);
    }
}
