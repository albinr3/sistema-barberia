namespace Barberia.Hardware.Pos;

public sealed class SimulatedCashBoxReceiptPrinter : ICashBoxReceiptPrinter
{
    private readonly HardwareOperationResult _result;

    public SimulatedCashBoxReceiptPrinter()
        : this(HardwareOperationResult.Success())
    {
    }

    public SimulatedCashBoxReceiptPrinter(HardwareOperationResult result)
    {
        _result = result ?? throw new ArgumentNullException(nameof(result));
    }

    public HardwareOperationResult Print(CashReceiptPrintJob job)
    {
        ArgumentNullException.ThrowIfNull(job);

        if (string.IsNullOrWhiteSpace(job.ReceiptNumber))
        {
            return HardwareOperationResult.Failure("Receipt number is required.");
        }

        if (job.DisplayTicketNumber <= 0)
        {
            return HardwareOperationResult.Failure("Ticket number is required.");
        }

        if (string.IsNullOrWhiteSpace(job.BarberName))
        {
            return HardwareOperationResult.Failure("Barber name is required.");
        }

        if (string.IsNullOrWhiteSpace(job.BarberStationCode))
        {
            return HardwareOperationResult.Failure("Barber station code is required.");
        }

        if (string.IsNullOrWhiteSpace(job.ServiceName))
        {
            return HardwareOperationResult.Failure("Service name is required.");
        }

        if (job.ServicePrice <= 0)
        {
            return HardwareOperationResult.Failure("Service price must be greater than zero.");
        }

        if (job.AdditionalAmount is not (0m or 2m or 3m or 5m))
        {
            return HardwareOperationResult.Failure("Service additional amount is invalid.");
        }

        if (string.IsNullOrWhiteSpace(job.DeviceId))
        {
            return HardwareOperationResult.Failure("Device id is required to print the cash receipt.");
        }

        if (job.Amount <= 0)
        {
            return HardwareOperationResult.Failure("Cash receipt amount must be greater than zero.");
        }

        return _result;
    }

    public HardwareOperationResult PrintDayReport(DayReportPrintJob job)
    {
        ArgumentNullException.ThrowIfNull(job);
        
        if (string.IsNullOrWhiteSpace(job.DeviceId))
        {
            return HardwareOperationResult.Failure("Device id is required to print the day report.");
        }

        return _result;
    }
}
