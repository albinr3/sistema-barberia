namespace Barberia.Hardware.Pos;

public sealed record QrCodeScanResult(bool Succeeded, string? Value = null, string? ErrorMessage = null)
{
    public static QrCodeScanResult Success(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("QR code value is required.", nameof(value));
        }

        return new QrCodeScanResult(true, value.Trim());
    }

    public static QrCodeScanResult Failure(string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
        {
            throw new ArgumentException("Error message is required.", nameof(errorMessage));
        }

        return new QrCodeScanResult(false, ErrorMessage: errorMessage.Trim());
    }
}
