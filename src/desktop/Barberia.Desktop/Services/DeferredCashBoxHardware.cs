using Barberia.Hardware.Pos;

namespace Barberia.Desktop.Services;

internal sealed class DeferredCashBoxReceiptPrinter : ICashBoxReceiptPrinter
{
    public HardwareOperationResult Print(CashReceiptPrintJob job)
    {
        return HardwareOperationResult.Failure("Receipt printing is deferred to the cash box station.");
    }

    public HardwareOperationResult PrintDayReport(DayReportPrintJob job)
    {
        return HardwareOperationResult.Failure("Day report printing is deferred to the cash box station.");
    }
}

internal sealed class DeferredCashDrawer : ICashDrawer
{
    public HardwareOperationResult Open(string deviceId)
    {
        return HardwareOperationResult.Failure("Cash drawer opening is deferred to the cash box station.");
    }
}
