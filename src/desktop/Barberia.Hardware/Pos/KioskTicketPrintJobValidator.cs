namespace Barberia.Hardware.Pos;

internal static class KioskTicketPrintJobValidator
{
    public static HardwareOperationResult Validate(KioskTicketPrintJob job)
    {
        ArgumentNullException.ThrowIfNull(job);

        if (job.DisplayTicketNumber <= 0)
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

        if (job.RequestedBarberStationCodes.Count != job.RequestedBarberNames.Count)
        {
            return HardwareOperationResult.Failure("Requested barber station codes must match requested barber names.");
        }

        if (string.IsNullOrWhiteSpace(job.DeviceId))
        {
            return HardwareOperationResult.Failure("Device id is required to print the kiosk ticket.");
        }

        return HardwareOperationResult.Success();
    }
}
