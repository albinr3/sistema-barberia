using Barberia.Data.Models;
using Barberia.Hardware.Pos;

namespace Barberia.Desktop.Services;

internal sealed class RemoteKioskStationService : LanClientBase, IKioskStationService
{
    private readonly StationSettings _settings;
    private readonly IKioskTicketPrinter _ticketPrinter;

    public RemoteKioskStationService(StationSettings settings)
        : this(settings, new WindowsGraphicsKioskTicketPrinter())
    {
    }

    internal RemoteKioskStationService(StationSettings settings, IKioskTicketPrinter ticketPrinter) : base(settings)
    {
        _settings = settings;
        _ticketPrinter = ticketPrinter;
    }

    public KioskCheckInSnapshot Load() => Get<KioskCheckInSnapshot>("/api/kiosk/snapshot");

    public KioskCheckInResult RegisterWalkIn(string customerName, bool acceptsAnyBarber, IReadOnlyCollection<Guid> requestedBarberIds)
    {
        var result = Post<KioskCheckInResult>("/api/kiosk/walk-ins", new LanKioskWalkInRequest(customerName, acceptsAnyBarber, requestedBarberIds.ToArray()));
        PrintTicketLocally(result, _ticketPrinter, _settings.DeviceId);
        return result;
    }

    internal static void PrintTicketLocally(KioskCheckInResult result, IKioskTicketPrinter ticketPrinter, string deviceId)
    {
        var printResult = ticketPrinter.Print(new KioskTicketPrintJob(
            result.DisplayTicketNumber,
            result.QrPayload,
            result.CustomerName,
            result.RequestedBarberNames,
            result.RequestedBarberStationCodes,
            result.AcceptsAnyBarber,
            result.AssignedBarberName,
            result.AssignedBarberStationCode,
            result.CheckedInAt,
            deviceId));

        if (!printResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"Ticket {result.DisplayTicketNumber} was registered on PC3 but PC1 could not print it: {printResult.ErrorMessage}");
        }
    }
}

internal sealed class RemoteBarberRotationStationService : LanClientBase, IBarberRotationStationService
{
    public RemoteBarberRotationStationService(StationSettings settings) : base(settings) { }

    public BarberCheckInSnapshot Load() => Get<BarberCheckInSnapshot>("/api/barber-rotation/snapshot");

    public BarberCheckInResult CheckIn(string stationInput)
    {
        return Post<BarberCheckInResult>("/api/barber-rotation/check-ins", new LanStationInputRequest(stationInput));
    }

    public void MarkBarberAvailable(Guid barberId)
    {
        Post("/api/barber-rotation/barbers/available", new LanBarberStateRequest(barberId));
    }

    public void MarkBarberOffline(Guid barberId)
    {
        Post("/api/barber-rotation/barbers/offline", new LanBarberStateRequest(barberId));
    }
}

internal sealed class RemoteCashBoxStationService : LanClientBase, ICashBoxStationService
{
    private readonly StationSettings _settings;
    private readonly ICashBoxReceiptPrinter _receiptPrinter = new WindowsGraphicsCashBoxReceiptPrinter();
    private readonly ICashDrawer _cashDrawer = new SimulatedCashDrawer();

    public RemoteCashBoxStationService(StationSettings settings) : base(settings)
    {
        _settings = settings;
    }

    public CashBoxSnapshot Load() => Get<CashBoxSnapshot>("/api/cashbox/snapshot");

    public CashBoxOpeningResult SaveOpeningBalance(decimal openingBalance)
    {
        return Post<CashBoxOpeningResult>("/api/cashbox/opening", new LanCashBoxOpeningRequest(openingBalance));
    }

    public CashBoxTicketLookupResult LookupTicket(string ticketNumber)
    {
        return Post<CashBoxTicketLookupResult>("/api/cashbox/ticket-lookup", new LanTicketInputRequest(ticketNumber));
    }

    public IReadOnlyList<PendingServicePaymentRow> ListPendingPayments()
    {
        return Get<IReadOnlyList<PendingServicePaymentRow>>("/api/cashbox/pending-payments");
    }

    public PendingPaymentCollectorLookupResult LookupPendingPaymentCollector(string stationNumberInput)
    {
        return Post<PendingPaymentCollectorLookupResult>("/api/cashbox/pending-payment-collector", new LanPendingPaymentCollectorRequest(stationNumberInput));
    }

    public PendingServicePaymentResult MarkServicePendingPayment(string ticketNumber, Guid serviceId, decimal additionalAmount)
    {
        return Post<PendingServicePaymentResult>("/api/cashbox/pay-later", new LanPendingPaymentRequest(ticketNumber, serviceId, additionalAmount));
    }

    public PendingPaymentCollectionResult CollectPendingPayments(IReadOnlyCollection<Guid> pendingPaymentIds, CustomerPaymentMethod paymentMethod, string? paymentReference, int collectorStationNumber, decimal tenderedAmount = 0, decimal changeAmount = 0)
    {
        var requestedIds = pendingPaymentIds.ToHashSet();
        var pendingRows = ListPendingPayments()
            .Where(row => requestedIds.Contains(row.Id))
            .ToArray();
        var result = Post<PendingPaymentCollectionResult>("/api/cashbox/collect-pending", new LanCollectPendingPaymentsRequest(pendingPaymentIds.ToArray(), paymentMethod, paymentReference, collectorStationNumber, tenderedAmount, changeAmount));
        var hardware = ExecuteLocalPendingCollectionHardware(result, pendingRows, paymentMethod, tenderedAmount, changeAmount);
        return result with
        {
            ReceiptPrinted = hardware.ReceiptPrinted,
            CashDrawerOpened = hardware.CashDrawerOpened,
            HardwareFailureMessage = hardware.FailureMessage
        };
    }

    public CashBoxDepositResult CloseService(string ticketNumber, Guid serviceId, decimal additionalAmount, CustomerPaymentMethod paymentMethod, string? paymentReference, decimal tenderedAmount = 0, decimal changeAmount = 0)
    {
        var result = Post<CashBoxDepositResult>("/api/cashbox/close-service", new LanCashBoxCloseServiceRequest(ticketNumber, serviceId, additionalAmount, paymentMethod, paymentReference, tenderedAmount, changeAmount));
        var hardware = ExecuteLocalCloseHardware(result, paymentMethod, tenderedAmount, changeAmount);
        return result with
        {
            ReceiptPrinted = hardware.ReceiptPrinted,
            CashDrawerOpened = hardware.CashDrawerOpened,
            HardwareFailureMessage = hardware.FailureMessage
        };
    }

    public void PrintDayReport()
    {
        var job = Get<DayReportPrintJob>("/api/cashbox/day-report-job");
        var result = _receiptPrinter.PrintDayReport(job);
        Post("/api/hardware-events", new LanHardwareEventRequest(
            StationRuntime.Current.Role.ToString(),
            _settings.DeviceId,
            "cashbox.day_report_print",
            result.Succeeded,
            result.ErrorMessage,
            null,
            null));

        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Could not print day report: {result.ErrorMessage}");
        }
    }


    private RemoteCashBoxHardwareResult ExecuteLocalPendingCollectionHardware(PendingPaymentCollectionResult result, IReadOnlyList<PendingServicePaymentRow> pendingRows, CustomerPaymentMethod paymentMethod, decimal tenderedAmount, decimal changeAmount)
    {
        if (pendingRows.Count == 0)
        {
            return new RemoteCashBoxHardwareResult(false, false, "Pending payment rows were not available for local receipt printing.");
        }

        var receiptPrinted = false;
        var cashDrawerOpened = false;
        string? failureMessage = null;
        var receiptLines = pendingRows
            .Select(row => new CashReceiptLine(
                row.DisplayTicketNumber,
                row.CustomerName,
                row.BarberName,
                row.BarberStationCode,
                row.ServiceName,
                row.ServicePrice,
                row.AdditionalAmount,
                row.Amount))
            .ToArray();

        try
        {
            var printResult = _receiptPrinter.Print(new CashReceiptPrintJob(
                result.ReceiptNumber,
                pendingRows[0].DisplayTicketNumber,
                pendingRows.Count == 1 ? pendingRows[0].BarberName : "Multiple barbers",
                pendingRows.Count == 1 ? pendingRows[0].BarberStationCode : "Group",
                pendingRows.Count == 1 ? pendingRows[0].ServiceName : $"{pendingRows.Count} pending services",
                pendingRows.Sum(row => row.ServicePrice),
                pendingRows.Sum(row => row.AdditionalAmount),
                result.TotalAmount,
                pendingRows.Sum(row => row.Commission),
                pendingRows[0].Currency,
                result.CollectedAt,
                _settings.DeviceId,
                paymentMethod.ToString(),
                receiptLines,
                result.CollectorBarberName,
                result.CollectorBarberStationCode,
                tenderedAmount,
                changeAmount));

            receiptPrinted = printResult.Succeeded;
            if (!printResult.Succeeded)
            {
                failureMessage = $"Printer failed: {printResult.ErrorMessage}";
            }
        }
        catch (Exception exception)
        {
            failureMessage = $"Printer error: {exception.Message}";
        }

        if (paymentMethod == CustomerPaymentMethod.Cash)
        {
            try
            {
                var drawerResult = _cashDrawer.Open(_settings.DeviceId);
                cashDrawerOpened = drawerResult.Succeeded;
                if (!drawerResult.Succeeded)
                {
                    var drawerMessage = $"Drawer failed: {drawerResult.ErrorMessage}";
                    failureMessage = failureMessage is null ? drawerMessage : $"{failureMessage} | {drawerMessage}";
                }
            }
            catch (Exception exception)
            {
                var drawerMessage = $"Drawer error: {exception.Message}";
                failureMessage = failureMessage is null ? drawerMessage : $"{failureMessage} | {drawerMessage}";
            }
        }

        Post("/api/hardware-events", new LanHardwareEventRequest(
            StationRuntime.Current.Role.ToString(),
            _settings.DeviceId,
            "cashbox.collect_pending_hardware",
            failureMessage is null,
            failureMessage,
            result.ReceiptNumber,
            pendingRows[0].DisplayTicketNumber));

        return new RemoteCashBoxHardwareResult(receiptPrinted, cashDrawerOpened, failureMessage);
    }
    private RemoteCashBoxHardwareResult ExecuteLocalCloseHardware(CashBoxDepositResult result, CustomerPaymentMethod paymentMethod, decimal tenderedAmount, decimal changeAmount)
    {
        var receiptPrinted = false;
        var cashDrawerOpened = false;
        string? failureMessage = null;

        try
        {
            var printResult = _receiptPrinter.Print(new CashReceiptPrintJob(
                result.ReceiptNumber,
                result.DisplayTicketNumber,
                result.BarberName,
                result.BarberStationCode,
                result.ServiceName,
                result.ServicePrice,
                result.AdditionalAmount,
                result.Amount,
                result.Commission,
                "USD",
                result.ClosedAt,
                _settings.DeviceId,
                paymentMethod.ToString(),
                null,
                null,
                null,
                tenderedAmount,
                changeAmount));

            receiptPrinted = printResult.Succeeded;
            if (!printResult.Succeeded)
            {
                failureMessage = $"Printer failed: {printResult.ErrorMessage}";
            }
        }
        catch (Exception exception)
        {
            failureMessage = $"Printer error: {exception.Message}";
        }

        if (paymentMethod == CustomerPaymentMethod.Cash)
        {
            try
            {
                var drawerResult = _cashDrawer.Open(_settings.DeviceId);
                cashDrawerOpened = drawerResult.Succeeded;
                if (!drawerResult.Succeeded)
                {
                    var drawerMessage = $"Drawer failed: {drawerResult.ErrorMessage}";
                    failureMessage = failureMessage is null ? drawerMessage : $"{failureMessage} | {drawerMessage}";
                }
            }
            catch (Exception exception)
            {
                var drawerMessage = $"Drawer error: {exception.Message}";
                failureMessage = failureMessage is null ? drawerMessage : $"{failureMessage} | {drawerMessage}";
            }
        }

        Post("/api/hardware-events", new LanHardwareEventRequest(
            StationRuntime.Current.Role.ToString(),
            _settings.DeviceId,
            "cashbox.close_service_hardware",
            failureMessage is null,
            failureMessage,
            result.ReceiptNumber,
            result.DisplayTicketNumber));

        return new RemoteCashBoxHardwareResult(receiptPrinted, cashDrawerOpened, failureMessage);
    }

    private sealed record RemoteCashBoxHardwareResult(bool ReceiptPrinted, bool CashDrawerOpened, string? FailureMessage);
}


