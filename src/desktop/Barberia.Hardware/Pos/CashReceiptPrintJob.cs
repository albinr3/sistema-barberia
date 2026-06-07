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
    string DeviceId)
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
        string deviceId)
        : this(
            receiptNumber,
            displayTicketNumber,
            barberName,
            barberStationCode,
            "Servicio no registrado",
            amount,
            0,
            amount,
            commission,
            currency,
            collectedAt,
            deviceId)
    {
    }
}
