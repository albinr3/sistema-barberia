using Barberia.Hardware.Pos;
using Xunit;

namespace Barberia.Hardware.Tests;

public sealed class PosHardwareTests
{
    [Fact]
    public void SimulatedKioskTicketPrinter_ReturnsSuccess()
    {
        var printer = new SimulatedKioskTicketPrinter();

        var result = printer.Print(new KioskTicketPrintJob(
            1,
            "W20260604120000000",
            "Mia",
            ["Luis"],
            ["B-1"],
            AcceptsAnyBarber: false,
            "Luis",
            "B-1",
            DateTimeOffset.Parse("2026-06-04T12:00:00Z"),
            "kiosk-1"));

        Assert.True(result.Succeeded);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void SimulatedKioskTicketPrinter_ReturnsFailureWithoutSelection()
    {
        var printer = new SimulatedKioskTicketPrinter();

        var result = printer.Print(new KioskTicketPrintJob(
            1,
            "W20260604120000000",
            "Mia",
            [],
            [],
            AcceptsAnyBarber: false,
            null,
            null,
            DateTimeOffset.Parse("2026-06-04T12:00:00Z"),
            "kiosk-1"));

        Assert.False(result.Succeeded);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public void SimulatedKioskTicketPrinter_ReturnsFailureWithoutQrPayload()
    {
        var printer = new SimulatedKioskTicketPrinter();

        var result = printer.Print(new KioskTicketPrintJob(
            1,
            "",
            "Mia",
            [],
            [],
            AcceptsAnyBarber: true,
            null,
            null,
            DateTimeOffset.Parse("2026-06-04T12:00:00Z"),
            "kiosk-1"));

        Assert.False(result.Succeeded);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public void SimulatedKioskTicketPrinter_CanSimulateDeviceFailure()
    {
        var printer = new SimulatedKioskTicketPrinter(
            HardwareOperationResult.Failure("Ticket printer offline."));

        var result = printer.Print(new KioskTicketPrintJob(
            1,
            "W20260604120000000",
            "Mia",
            [],
            [],
            AcceptsAnyBarber: true,
            null,
            null,
            DateTimeOffset.Parse("2026-06-04T12:00:00Z"),
            "kiosk-1"));

        Assert.False(result.Succeeded);
        Assert.Equal("Ticket printer offline.", result.ErrorMessage);
    }

    [Fact]
    public void WindowsGraphicsKioskTicketPrinter_ReturnsValidationFailureBeforePrinting()
    {
        var printer = new WindowsGraphicsKioskTicketPrinter();

        var result = printer.Print(new KioskTicketPrintJob(
            1,
            "",
            "Mia",
            [],
            [],
            AcceptsAnyBarber: true,
            null,
            null,
            DateTimeOffset.Parse("2026-06-04T12:00:00Z"),
            "kiosk-1"));

        Assert.False(result.Succeeded);
        Assert.Equal("QR payload is required.", result.ErrorMessage);
    }

    [Fact]
    public void QrCodeMatrix_CreatesVersionOneQrForTicketPayload()
    {
        var matrix = QrCodeMatrix.CreateAlphanumeric("W20260604120000000");
        var darkModules = 0;
        var lightModules = 0;

        for (var y = 0; y < matrix.Size; y++)
        {
            for (var x = 0; x < matrix.Size; x++)
            {
                if (matrix.IsDark(x, y))
                {
                    darkModules++;
                }
                else
                {
                    lightModules++;
                }
            }
        }

        Assert.Equal(21, matrix.Size);
        Assert.True(matrix.IsDark(0, 0));
        Assert.True(matrix.IsDark(6, 0));
        Assert.True(matrix.IsDark(14, 0));
        Assert.True(matrix.IsDark(0, 14));
        Assert.True(darkModules > 0);
        Assert.True(lightModules > 0);
    }

    [Fact]
    public void KioskTicketPrintText_UsesMasterClipsAndEnglishCopy()
    {
        var printedText = string.Join(
            Environment.NewLine,
            KioskTicketPrintText.BrandName,
            KioskTicketPrintText.CodeLabel,
            KioskTicketPrintText.AnyAvailableBarber,
            KioskTicketPrintText.RequestedBarbers,
            KioskTicketPrintText.Assigned,
            KioskTicketPrintText.PresentTicket,
            KioskTicketPrintText.ThankYou);

        Assert.Contains("MASTERCLIPS\nBARBER SHOP", printedText);
        Assert.Contains("Please present this ticket to your barber.", printedText);
        Assert.Contains("Thank you for your visit.", printedText);
        Assert.DoesNotContain("BARBERIA", printedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Codigo", printedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Barbero", printedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Gracias", printedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SimulatedCashBoxReceiptPrinter_ReturnsSuccess()
    {
        var printer = new SimulatedCashBoxReceiptPrinter();

        var result = printer.Print(new CashReceiptPrintJob(
            "CB-001",
            1,
            "Luis",
            "B-1",
            25m,
            5m,
            "USD",
            DateTimeOffset.Parse("2026-06-04T12:00:00Z"),
            "autocaja-1"));

        Assert.True(result.Succeeded);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void SimulatedCashBoxReceiptPrinter_AcceptsPendingPaymentReceiptLines()
    {
        var printer = new SimulatedCashBoxReceiptPrinter();
        var lines = new[]
        {
            new CashReceiptLine(1, "Ana", "Luis", "B-1", "Kids Cut", 23m, 2m, 25m),
            new CashReceiptLine(2, "Mia", "Frank", "B-2", "Regular Cut", 20m, 0m, 20m)
        };

        var result = printer.Print(new CashReceiptPrintJob(
            "CB-001",
            1,
            "Multiple barbers",
            "Group",
            "2 pending services",
            43m,
            2m,
            45m,
            29.25m,
            "USD",
            DateTimeOffset.Parse("2026-06-04T12:00:00Z"),
            "autocaja-1",
            "Cash",
            lines,
            "Franklin",
            "B-9"));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void SimulatedCashBoxReceiptPrinter_RejectsPendingPaymentReceiptTotalMismatch()
    {
        var printer = new SimulatedCashBoxReceiptPrinter();
        var lines = new[]
        {
            new CashReceiptLine(1, "Ana", "Luis", "B-1", "Kids Cut", 23m, 2m, 25m),
            new CashReceiptLine(2, "Mia", "Frank", "B-2", "Regular Cut", 20m, 0m, 20m)
        };

        var result = printer.Print(new CashReceiptPrintJob(
            "CB-001",
            1,
            "Multiple barbers",
            "Group",
            "2 pending services",
            43m,
            2m,
            40m,
            29.25m,
            "USD",
            DateTimeOffset.Parse("2026-06-04T12:00:00Z"),
            "autocaja-1",
            "Cash",
            lines,
            "Franklin",
            "B-9"));

        Assert.False(result.Succeeded);
        Assert.Equal("Receipt line totals must match receipt total.", result.ErrorMessage);
    }
    [Fact]
    public void SimulatedCashBoxReceiptPrinter_ReturnsFailureForInvalidJob()
    {
        var printer = new SimulatedCashBoxReceiptPrinter();

        var result = printer.Print(new CashReceiptPrintJob(
            "",
            1,
            "Luis",
            "B-1",
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
            1,
            "Luis",
            "B-1",
            25m,
            5m,
            "USD",
            DateTimeOffset.Parse("2026-06-04T12:00:00Z"),
            "autocaja-1"));

        Assert.False(result.Succeeded);
        Assert.Equal("Printer offline.", result.ErrorMessage);
    }


    [Fact]
    public void WindowsGraphicsCashBoxReceiptPrinter_ReturnsValidationFailureBeforePrintingReceipt()
    {
        var printer = new WindowsGraphicsCashBoxReceiptPrinter();

        var result = printer.Print(new CashReceiptPrintJob(
            "",
            1,
            "Luis",
            "B-1",
            25m,
            5m,
            "USD",
            DateTimeOffset.Parse("2026-06-04T12:00:00Z"),
            "autocaja-1"));

        Assert.False(result.Succeeded);
        Assert.Equal("Receipt number is required.", result.ErrorMessage);
    }

    [Fact]
    public void WindowsGraphicsCashBoxReceiptPrinter_ReturnsValidationFailureBeforePrintingDayReport()
    {
        var printer = new WindowsGraphicsCashBoxReceiptPrinter();

        var result = printer.PrintDayReport(new DayReportPrintJob(
            25m,
            100m,
            25m,
            0m,
            125m,
            [new BarberDayReport("Luis (B-1)", 1, 25m)],
            DateTimeOffset.Parse("2026-06-04T12:00:00Z"),
            ""));

        Assert.False(result.Succeeded);
        Assert.Equal("Device id is required to print the day report.", result.ErrorMessage);
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
