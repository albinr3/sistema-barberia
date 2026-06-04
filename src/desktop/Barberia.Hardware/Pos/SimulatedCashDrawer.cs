namespace Barberia.Hardware.Pos;

public sealed class SimulatedCashDrawer : ICashDrawer
{
    public HardwareOperationResult Open(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return HardwareOperationResult.Failure("Device id is required to open the cash drawer.");
        }

        return HardwareOperationResult.Success();
    }
}
