using Barberia.Data.Models;
using System;

namespace Barberia.Desktop.Services;

public class CashBoxCustomerDisplayState
{
    public static CashBoxCustomerDisplayState Current { get; } = new();

    public event EventHandler? StateChanged;

    public string DisplayState { get; private set; } = "Idle"; // Idle, TicketLoaded, ServiceSelected, Completed, PendingPayment

    public string? TicketNumber { get; private set; }
    public string? CustomerFirstName { get; private set; }
    public string? BarberName { get; private set; }
    public string? StationCode { get; private set; }

    public string? ServiceName { get; private set; }
    public decimal ServicePrice { get; private set; }
    public decimal AdditionalAmount { get; private set; }
    public decimal TotalAmount => ServicePrice + AdditionalAmount;

    public CustomerPaymentMethod? PaymentMethod { get; private set; }
    public decimal TenderedAmount { get; private set; }
    public decimal ChangeAmount { get; private set; }

    public void SetIdle()
    {
        DisplayState = "Idle";
        ClearData();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetTicketLoaded(string ticketNumber, string customerName, string barberName, string stationCode)
    {
        DisplayState = "TicketLoaded";
        TicketNumber = ticketNumber;
        CustomerFirstName = ExtractFirstName(customerName);
        BarberName = barberName;
        StationCode = stationCode;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetServiceSelected(string serviceName, decimal servicePrice, decimal additionalAmount)
    {
        DisplayState = "ServiceSelected";
        ServiceName = serviceName;
        ServicePrice = servicePrice;
        AdditionalAmount = additionalAmount;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void UpdateAdditionalAmount(decimal additionalAmount)
    {
        AdditionalAmount = additionalAmount;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void UpdatePaymentMethod(CustomerPaymentMethod method)
    {
        PaymentMethod = method;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void UpdateCashPayment(decimal tendered, decimal change)
    {
        TenderedAmount = tendered;
        ChangeAmount = change;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetCompleted()
    {
        DisplayState = "Completed";
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetPendingPayment()
    {
        DisplayState = "PendingPayment";
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ClearData()
    {
        TicketNumber = null;
        CustomerFirstName = null;
        BarberName = null;
        StationCode = null;
        ServiceName = null;
        ServicePrice = 0;
        AdditionalAmount = 0;
        PaymentMethod = null;
        TenderedAmount = 0;
        ChangeAmount = 0;
    }

    private string ExtractFirstName(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName) || fullName.Contains("Walk-in", StringComparison.OrdinalIgnoreCase)) 
            return "Walk-in customer";
        var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[0] : "Walk-in customer";
    }
}
