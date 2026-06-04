namespace Barberia.Hardware.Pos;

public sealed record HardwareOperationResult(bool Succeeded, string? ErrorMessage = null)
{
    public static HardwareOperationResult Success()
    {
        return new HardwareOperationResult(true);
    }

    public static HardwareOperationResult Failure(string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
        {
            throw new ArgumentException("Error message is required.", nameof(errorMessage));
        }

        return new HardwareOperationResult(false, errorMessage.Trim());
    }
}
