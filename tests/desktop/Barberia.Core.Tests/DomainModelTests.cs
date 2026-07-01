using System.Text.Json;
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
            TurnState.Called,
            TurnState.InService,
            TurnState.Completed,
            TurnState.Cancelled,
            TurnState.NoShow,
            TurnState.Voided,
        ];

        Assert.Equal(7, states.Distinct().Count());
        Assert.Equal(0, (int)TurnState.Waiting);
        Assert.False(Enum.IsDefined(typeof(TurnState), 1));
        Assert.Equal(2, (int)TurnState.Called);
    }

    [Fact]
    public void Turn_NormalizesCustomerNameAndRequestedBarbersWithoutDuplicatingIds()
    {
        var barberId = Guid.NewGuid();

        var turn = new Turn(
            Guid.NewGuid(),
            " A-001 ",
            1,
            DateOnly.Parse("2026-06-03"),
            TurnState.Waiting,
            TurnSource.WalkIn,
            DateTimeOffset.UtcNow,
            requestedBarberIds: [barberId, barberId],
            customerName: " Mia ");

        Assert.Equal("A-001", turn.TicketNumber);
        Assert.Equal(1, turn.DisplayTicketNumber);
        Assert.Equal(DateOnly.Parse("2026-06-03"), turn.TicketDate);
        Assert.Equal("Mia", turn.CustomerName);
        Assert.Equal([barberId], turn.RequestedBarberIds);
    }

    [Fact]
    public void Turn_RejectsInvalidDisplayTicketNumber()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Turn(
                Guid.NewGuid(),
                "A-001",
                0,
                DateOnly.Parse("2026-06-03"),
                TurnState.Waiting,
                TurnSource.WalkIn,
                DateTimeOffset.Parse("2026-06-03T12:00:00Z")));
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
        Assert.Equal(Barber.DefaultCommissionPercentage, barber.CommissionPercentage);
        Assert.Equal(0.65m, barber.CommissionRate);
    }

    [Fact]
    public void Barber_StoresCommissionPercentage()
    {
        var barber = new Barber(
            Guid.NewGuid(),
            "Marcus",
            BarberState.Available,
            0,
            1,
            stationNumber: 3,
            commissionPercentage: 70);

        Assert.Equal(70, barber.CommissionPercentage);
        Assert.Equal(0.70m, barber.CommissionRate);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Barber_RejectsInvalidCommissionPercentage(int commissionPercentage)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Barber(
                Guid.NewGuid(),
                "Marcus",
                BarberState.Available,
                0,
                1,
                stationNumber: 1,
                commissionPercentage: commissionPercentage));
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
    public void Barber_RoundTripsThroughSystemTextJson()
    {
        var updatedAt = DateTimeOffset.Parse("2026-06-30T21:15:00-04:00");
        var barber = new Barber(
            Guid.NewGuid(),
            "Marcus",
            BarberState.Available,
            2,
            1,
            checkedInAt: updatedAt.AddHours(-1),
            stationNumber: 3,
            profileImagePath: "Assets/barber1.png",
            isActive: true,
            commissionPercentage: 70,
            updatedAt: updatedAt);

        var json = JsonSerializer.Serialize(barber);
        var deserialized = JsonSerializer.Deserialize<Barber>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(barber.Id, deserialized.Id);
        Assert.Equal(barber.DisplayName, deserialized.DisplayName);
        Assert.Equal(barber.State, deserialized.State);
        Assert.Equal(barber.StationNumber, deserialized.StationNumber);
        Assert.Equal(barber.UpdatedAt, deserialized.UpdatedAt);
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
