namespace Barberia.Hardware.Pos;

public interface IQrCodeScanner
{
    QrCodeScanResult Scan(string deviceId);
}
