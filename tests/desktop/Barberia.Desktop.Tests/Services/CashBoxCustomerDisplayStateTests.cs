using Barberia.Data.Models;
using Barberia.Desktop.Services;
using Xunit;

namespace Barberia.Desktop.Tests.Services;

public class CashBoxCustomerDisplayStateTests
{
    public CashBoxCustomerDisplayStateTests()
    {
        // Reset state before each test
        CashBoxCustomerDisplayState.Current.SetIdle();
    }

    [Fact]
    public void SetIdle_ClearsAllData_AndSetsStateToIdle()
    {
        var state = CashBoxCustomerDisplayState.Current;
        state.SetTicketLoaded("T01", "John Doe", "Luis", "1");
        
        state.SetIdle();

        Assert.Equal("Idle", state.DisplayState);
        Assert.Null(state.TicketNumber);
        Assert.Null(state.CustomerFirstName);
        Assert.Null(state.BarberName);
        Assert.Null(state.StationCode);
        Assert.Null(state.ServiceName);
        Assert.Equal(0, state.ServicePrice);
        Assert.Equal(0, state.AdditionalAmount);
        Assert.Equal(0, state.TotalAmount);
        Assert.Null(state.PaymentMethod);
        Assert.Equal(0, state.TenderedAmount);
        Assert.Equal(0, state.ChangeAmount);
    }

    [Fact]
    public void SetTicketLoaded_ExtractsFirstName_AndSetsBasicInfo()
    {
        var state = CashBoxCustomerDisplayState.Current;
        
        state.SetTicketLoaded("T01", "John Doe Smith", "Luis", "1");

        Assert.Equal("TicketLoaded", state.DisplayState);
        Assert.Equal("T01", state.TicketNumber);
        Assert.Equal("John", state.CustomerFirstName);
        Assert.Equal("Luis", state.BarberName);
        Assert.Equal("1", state.StationCode);
    }

    [Fact]
    public void SetTicketLoaded_WithWalkInCustomer_SetsWalkInCustomer()
    {
        var state = CashBoxCustomerDisplayState.Current;
        
        state.SetTicketLoaded("T02", "Walk-in Customer", "Pedro", "2");

        Assert.Equal("Walk-in customer", state.CustomerFirstName);
    }

    [Fact]
    public void SetServiceSelected_CalculatesTotalAmount()
    {
        var state = CashBoxCustomerDisplayState.Current;
        
        state.SetServiceSelected("Haircut", 25.00m, 5.00m);

        Assert.Equal("ServiceSelected", state.DisplayState);
        Assert.Equal("Haircut", state.ServiceName);
        Assert.Equal(25.00m, state.ServicePrice);
        Assert.Equal(5.00m, state.AdditionalAmount);
        Assert.Equal(30.00m, state.TotalAmount);
    }

    [Fact]
    public void UpdateCashPayment_UpdatesTenderedAndChange()
    {
        var state = CashBoxCustomerDisplayState.Current;
        
        state.UpdateCashPayment(40.00m, 10.00m);

        Assert.Equal(40.00m, state.TenderedAmount);
        Assert.Equal(10.00m, state.ChangeAmount);
    }

    [Fact]
    public void SetCompleted_UpdatesState()
    {
        var state = CashBoxCustomerDisplayState.Current;
        
        state.SetCompleted();

        Assert.Equal("Completed", state.DisplayState);
    }

    [Fact]
    public void SetPendingPayment_UpdatesState()
    {
        var state = CashBoxCustomerDisplayState.Current;
        
        state.SetPendingPayment();

        Assert.Equal("PendingPayment", state.DisplayState);
    }
}
