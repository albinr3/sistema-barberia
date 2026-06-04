using Barberia.Hardware.Pos;
using Xunit;

namespace Barberia.Hardware.Tests;

public sealed class PosHardwareTests
{
    [Fact]
    public void SimulatedCashBoxReceiptPrinter_ReturnsSuccess()
    {
        var printer = new SimulatedCashBoxReceiptPrinter();

        var result = printer.Print(new CashReceiptPrintJob(
            "CB-001",
            "A-001",
            "Luis",
            25m,
            5m,
            "USD",
            DateTimeOffset.Parse("2026-06-04T12:00:00Z"),
            "autocaja-1"));

        Assert.True(result.Succeeded);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void SimulatedCashDrawer_ReturnsFailureWithoutDeviceId()
    {
        var drawer = new SimulatedCashDrawer();

        var result = drawer.Open("");

        Assert.False(result.Succeeded);
        Assert.NotNull(result.ErrorMessage);
    }
}
