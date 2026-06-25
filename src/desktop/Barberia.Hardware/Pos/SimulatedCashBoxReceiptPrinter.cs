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

        var validation = ValidateReceipt(job);
        return validation.Succeeded ? _result : validation;
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

    private static HardwareOperationResult ValidateReceipt(CashReceiptPrintJob job)
    {
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

        if (job.AdditionalAmount < 0)
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

        return ValidateLines(job);
    }

    private static HardwareOperationResult ValidateLines(CashReceiptPrintJob job)
    {
        if (job.Lines is not { Count: > 0 } lines)
        {
            return HardwareOperationResult.Success();
        }

        if (string.IsNullOrWhiteSpace(job.CollectedByName) || string.IsNullOrWhiteSpace(job.CollectedByStationCode))
        {
            return HardwareOperationResult.Failure("Collector barber is required for pending payments receipt.");
        }

        foreach (var line in lines)
        {
            if (line.DisplayTicketNumber <= 0)
            {
                return HardwareOperationResult.Failure("Receipt line ticket number is required.");
            }

            if (string.IsNullOrWhiteSpace(line.BarberName) || string.IsNullOrWhiteSpace(line.BarberStationCode))
            {
                return HardwareOperationResult.Failure("Receipt line barber is required.");
            }

            if (string.IsNullOrWhiteSpace(line.ServiceName))
            {
                return HardwareOperationResult.Failure("Receipt line service is required.");
            }

            if (line.ServicePrice <= 0 || line.AdditionalAmount < 0 || line.Amount <= 0)
            {
                return HardwareOperationResult.Failure("Receipt line amount is invalid.");
            }
        }

        var lineTotal = lines.Sum(line => line.Amount);
        return lineTotal == job.Amount
            ? HardwareOperationResult.Success()
            : HardwareOperationResult.Failure("Receipt line totals must match receipt total.");
    }
}
