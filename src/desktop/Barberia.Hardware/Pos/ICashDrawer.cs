namespace Barberia.Hardware.Pos;

public interface ICashDrawer
{
    HardwareOperationResult Open(string deviceId);
}
