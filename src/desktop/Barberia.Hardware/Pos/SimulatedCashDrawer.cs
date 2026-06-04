namespace Barberia.Hardware.Pos;

public sealed class SimulatedCashDrawer : ICashDrawer
{
    private readonly HardwareOperationResult _result;

    public SimulatedCashDrawer()
        : this(HardwareOperationResult.Success())
    {
    }

    public SimulatedCashDrawer(HardwareOperationResult result)
    {
        _result = result ?? throw new ArgumentNullException(nameof(result));
    }

    public HardwareOperationResult Open(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return HardwareOperationResult.Failure("Device id is required to open the cash drawer.");
        }

        return _result;
    }
}
