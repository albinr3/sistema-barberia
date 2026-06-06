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
    public void Turn_NormalizesCustomerNameAndRequestedBarbersWithoutDuplicatingIds()
    {
        var barberId = Guid.NewGuid();

        var turn = new Turn(
            Guid.NewGuid(),
            " A-001 ",
            TurnState.Waiting,
            TurnSource.WalkIn,
            DateTimeOffset.UtcNow,
            requestedBarberIds: [barberId, barberId],
            customerName: " Mia ");

        Assert.Equal("A-001", turn.TicketNumber);
        Assert.Equal("Mia", turn.CustomerName);
        Assert.Equal([barberId], turn.RequestedBarberIds);
    }

    [Fact]
    public void Barber_NormalizesOptionalProfileImagePath()
    {
        var barber = new Barber(
            Guid.NewGuid(),
            " Marcus ",
            BarberState.Available,
            0,
            1,
            profileImagePath: " Assets/barber1.png ",
            isActive: false);

        Assert.Equal("Marcus", barber.DisplayName);
        Assert.Equal("Assets/barber1.png", barber.ProfileImagePath);
        Assert.False(barber.IsActive);
    }

    [Fact]
    public void Barber_StoresStationForActiveBarber()
    {
        var barber = new Barber(
            Guid.NewGuid(),
            "Marcus",
            BarberState.Available,
            0,
            1,
            stationNumber: 3);

        Assert.Equal(3, barber.StationNumber);
        Assert.Equal("B-3", barber.StationCode);
        Assert.Equal("B-3 - Marcus", barber.DisplayNameWithStation);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Barber_RejectsInvalidStationNumbers(int stationNumber)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Barber(
                Guid.NewGuid(),
                "Marcus",
                BarberState.Available,
                0,
                1,
                stationNumber: stationNumber));
    }

    [Fact]
    public void Barber_RejectsActiveBarberWithoutStation()
    {
        Assert.Throws<ArgumentException>(() =>
            new Barber(
                Guid.NewGuid(),
                "Marcus",
                BarberState.Available,
                0,
                1));
    }

    [Fact]
    public void Barber_AllowsInactiveBarberWithoutStation()
    {
        var barber = new Barber(
            Guid.NewGuid(),
            "Marcus",
            BarberState.Offline,
            0,
            1,
            isActive: false);

        Assert.Null(barber.StationNumber);
        Assert.Null(barber.StationCode);
        Assert.Equal("Marcus", barber.DisplayNameWithStation);
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
