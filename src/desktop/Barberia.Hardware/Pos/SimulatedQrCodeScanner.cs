namespace Barberia.Hardware.Pos;

public sealed class SimulatedQrCodeScanner : IQrCodeScanner
{
    private readonly QrCodeScanResult _result;

    public SimulatedQrCodeScanner(string scannedValue)
        : this(QrCodeScanResult.Success(scannedValue))
    {
    }

    public SimulatedQrCodeScanner(QrCodeScanResult result)
    {
        _result = result ?? throw new ArgumentNullException(nameof(result));
    }

    public QrCodeScanResult Scan(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return QrCodeScanResult.Failure("Device id is required to scan a QR code.");
        }

        return _result;
    }
}
