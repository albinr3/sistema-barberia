namespace Barberia.Hardware.Pos;

public interface ICashBoxReceiptPrinter
{
    HardwareOperationResult Print(CashReceiptPrintJob job);
}
