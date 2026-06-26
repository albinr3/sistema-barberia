using Barberia.Core.Domain;
using Barberia.Desktop.Views;
using Xunit;

namespace Barberia.Desktop.Tests;

public sealed class PublicDisplayPageTests
{
    [Fact]
    public void OrderAvailableBarbersForStatus_PrioritizesZeroClientBarbersAndKeepsQueueOrder()
    {
        var now = new DateTimeOffset(2026, 6, 26, 9, 0, 0, TimeSpan.Zero);
        var b2 = AvailableBarber("B-2", stationNumber: 2, clientsServedToday: 2, now.AddMinutes(1));
        var b3 = AvailableBarber("B-3", stationNumber: 3, clientsServedToday: 1, now.AddMinutes(2));
        var b5 = AvailableBarber("B-5", stationNumber: 5, clientsServedToday: 0, now.AddMinutes(3));
        var b6 = AvailableBarber("B-6", stationNumber: 6, clientsServedToday: 0, now.AddMinutes(4));

        var ordered = PublicDisplayPage.OrderAvailableBarbersForStatus([b2, b3, b5, b6]);

        Assert.Equal([b5.Id, b6.Id, b2.Id, b3.Id], ordered.Select(barber => barber.Id));
    }

    private static Barber AvailableBarber(
        string displayName,
        int stationNumber,
        int clientsServedToday,
        DateTimeOffset checkedInAt)
    {
        return new Barber(
            Guid.NewGuid(),
            displayName,
            BarberState.Available,
            clientsServedToday,
            stationNumber - 1,
            checkedInAt,
            stationNumber: stationNumber);
    }
}
