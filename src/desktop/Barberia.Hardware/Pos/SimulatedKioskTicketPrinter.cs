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
        var validation = KioskTicketPrintJobValidator.Validate(job);
        if (!validation.Succeeded)
        {
            return validation;
        }

        return _result;
    }
}
