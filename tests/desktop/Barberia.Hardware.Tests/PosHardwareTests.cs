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
    public void SimulatedCashBoxReceiptPrinter_ReturnsFailureForInvalidJob()
    {
        var printer = new SimulatedCashBoxReceiptPrinter();

        var result = printer.Print(new CashReceiptPrintJob(
            "",
            "A-001",
            "Luis",
            25m,
            5m,
            "USD",
            DateTimeOffset.Parse("2026-06-04T12:00:00Z"),
            "autocaja-1"));

        Assert.False(result.Succeeded);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public void SimulatedCashBoxReceiptPrinter_CanSimulateDeviceFailure()
    {
        var printer = new SimulatedCashBoxReceiptPrinter(
            HardwareOperationResult.Failure("Printer offline."));

        var result = printer.Print(new CashReceiptPrintJob(
            "CB-001",
            "A-001",
            "Luis",
            25m,
            5m,
            "USD",
            DateTimeOffset.Parse("2026-06-04T12:00:00Z"),
            "autocaja-1"));

        Assert.False(result.Succeeded);
        Assert.Equal("Printer offline.", result.ErrorMessage);
    }

    [Fact]
    public void SimulatedCashDrawer_ReturnsSuccessWithDeviceId()
    {
        var drawer = new SimulatedCashDrawer();

        var result = drawer.Open("autocaja-1");

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

    [Fact]
    public void SimulatedCashDrawer_CanSimulateDeviceFailure()
    {
        var drawer = new SimulatedCashDrawer(
            HardwareOperationResult.Failure("Drawer jammed."));

        var result = drawer.Open("autocaja-1");

        Assert.False(result.Succeeded);
        Assert.Equal("Drawer jammed.", result.ErrorMessage);
    }

    [Fact]
    public void SimulatedQrCodeScanner_ReturnsScannedValue()
    {
        var scanner = new SimulatedQrCodeScanner("A-001");

        var result = scanner.Scan("scanner-1");

        Assert.True(result.Succeeded);
        Assert.Equal("A-001", result.Value);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void SimulatedQrCodeScanner_ReturnsFailureWithoutDeviceId()
    {
        var scanner = new SimulatedQrCodeScanner("A-001");

        var result = scanner.Scan("");

        Assert.False(result.Succeeded);
        Assert.Null(result.Value);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public void SimulatedQrCodeScanner_CanSimulateReadFailure()
    {
        var scanner = new SimulatedQrCodeScanner(
            QrCodeScanResult.Failure("Unreadable QR code."));

        var result = scanner.Scan("scanner-1");

        Assert.False(result.Succeeded);
        Assert.Null(result.Value);
        Assert.Equal("Unreadable QR code.", result.ErrorMessage);
    }
}
