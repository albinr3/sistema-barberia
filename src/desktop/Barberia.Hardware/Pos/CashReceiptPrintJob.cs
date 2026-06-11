namespace Barberia.Hardware.Pos;

public sealed record CashReceiptPrintJob(
    string ReceiptNumber,
    int DisplayTicketNumber,
    string BarberName,
    string BarberStationCode,
    string ServiceName,
    decimal ServicePrice,
    decimal AdditionalAmount,
    decimal Amount,
    decimal Commission,
    string Currency,
    DateTimeOffset CollectedAt,
    string DeviceId,
    string PaymentMethod = "Cash")
{
    public CashReceiptPrintJob(
        string receiptNumber,
        int displayTicketNumber,
        string barberName,
        string barberStationCode,
        decimal amount,
        decimal commission,
        string currency,
        DateTimeOffset collectedAt,
        string deviceId,
        string paymentMethod = "Cash")
        : this(
            receiptNumber,
            displayTicketNumber,
            barberName,
            barberStationCode,
            "Service not registered",
            amount,
            0,
            amount,
            commission,
            currency,
            collectedAt,
            deviceId,
            paymentMethod)
    {
    }
}
