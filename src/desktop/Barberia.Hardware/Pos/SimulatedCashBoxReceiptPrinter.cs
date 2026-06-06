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
}
