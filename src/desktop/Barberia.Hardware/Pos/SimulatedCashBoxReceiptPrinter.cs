namespace Barberia.Hardware.Pos;

public sealed class SimulatedCashBoxReceiptPrinter : ICashBoxReceiptPrinter
{
    public HardwareOperationResult Print(CashReceiptPrintJob job)
    {
        ArgumentNullException.ThrowIfNull(job);
        return HardwareOperationResult.Success();
    }
}
