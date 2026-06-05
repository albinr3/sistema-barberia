namespace Barberia.Hardware.Pos;

public sealed class SimulatedKioskTicketPrinter : IKioskTicketPrinter
{
    private readonly HardwareOperationResult _result;

    public SimulatedKioskTicketPrinter()
        : this(HardwareOperationResult.Success())
    {
    }

    public SimulatedKioskTicketPrinter(HardwareOperationResult result)
    {
        _result = result ?? throw new ArgumentNullException(nameof(result));
    }

    public HardwareOperationResult Print(KioskTicketPrintJob job)
    {
        ArgumentNullException.ThrowIfNull(job);

        if (string.IsNullOrWhiteSpace(job.TicketNumber))
        {
            return HardwareOperationResult.Failure("Ticket number is required.");
        }

        if (string.IsNullOrWhiteSpace(job.QrPayload))
        {
            return HardwareOperationResult.Failure("QR payload is required.");
        }

        if (string.IsNullOrWhiteSpace(job.CustomerName))
        {
            return HardwareOperationResult.Failure("Customer name is required.");
        }

        if (!job.AcceptsAnyBarber && job.RequestedBarberNames.Count == 0)
        {
            return HardwareOperationResult.Failure("At least one requested barber is required.");
        }

        if (string.IsNullOrWhiteSpace(job.DeviceId))
        {
            return HardwareOperationResult.Failure("Device id is required to print the kiosk ticket.");
        }

        return _result;
    }
}
