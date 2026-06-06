namespace Barberia.Hardware.Pos;

public interface IKioskTicketPrinter
{
    HardwareOperationResult Print(KioskTicketPrintJob job);
}
