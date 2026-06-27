using Barberia.Data.Models;
using Barberia.Desktop.Services;
using Microsoft.UI.Xaml;
using System;
using System.Threading.Tasks;

namespace Barberia.Desktop.Views;

public sealed partial class CustomerCashBoxWindow : Window
{
    public CustomerCashBoxWindow()
    {
        InitializeComponent();
        
        AppWindow.Title = "Customer Display";
        
        CashBoxCustomerDisplayState.Current.StateChanged += OnStateChanged;
        
        // Initial render
        RenderState();
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        // Ensure UI updates happen on the correct thread
        DispatcherQueue.TryEnqueue(RenderState);
    }

    private async void RenderState()
    {
        var state = CashBoxCustomerDisplayState.Current;

        switch (state.DisplayState)
        {
            case "Idle":
                IdlePanel.Visibility = Visibility.Visible;
                ActivePanel.Visibility = Visibility.Collapsed;
                FinalStatePanel.Visibility = Visibility.Collapsed;
                FooterText.Visibility = Visibility.Collapsed;
                break;

            case "TicketLoaded":
                IdlePanel.Visibility = Visibility.Collapsed;
                ActivePanel.Visibility = Visibility.Visible;
                ServiceInfoGrid.Visibility = Visibility.Collapsed;
                PaymentInfoGrid.Visibility = Visibility.Collapsed;
                FinalStatePanel.Visibility = Visibility.Collapsed;
                FooterText.Visibility = Visibility.Visible;
                FooterText.Text = "Please review your ticket before payment";
                FooterText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 100, 116, 139)); // #64748B

                CustomerNameText.Text = state.CustomerFirstName ?? "Walk-in customer";
                BarberInfoText.Text = $"{state.StationCode} - {state.BarberName}";
                break;

            case "ServiceSelected":
                IdlePanel.Visibility = Visibility.Collapsed;
                ActivePanel.Visibility = Visibility.Visible;
                ServiceInfoGrid.Visibility = Visibility.Visible;
                PaymentInfoGrid.Visibility = Visibility.Visible; // We show it here to display changes
                FinalStatePanel.Visibility = Visibility.Collapsed;
                FooterText.Visibility = Visibility.Visible;
                FooterText.Text = "Please review your ticket before payment";
                FooterText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 100, 116, 139)); // #64748B

                CustomerNameText.Text = state.CustomerFirstName ?? "Walk-in customer";
                BarberInfoText.Text = $"{state.StationCode} - {state.BarberName}";
                
                ServiceNameText.Text = state.ServiceName ?? "";
                ServicePriceText.Text = $"${state.ServicePrice:0.00}";
                
                if (state.AdditionalAmount > 0)
                {
                    AdditionLabelText.Visibility = Visibility.Visible;
                    AdditionalAmountText.Visibility = Visibility.Visible;
                    AdditionalAmountText.Text = $"${state.AdditionalAmount:0.00}";
                }
                else
                {
                    AdditionLabelText.Visibility = Visibility.Collapsed;
                    AdditionalAmountText.Visibility = Visibility.Collapsed;
                }
                
                TotalAmountText.Text = $"${state.TotalAmount:0.00}";

                UpdatePaymentView(state);
                break;

            case "Completed":
                IdlePanel.Visibility = Visibility.Collapsed;
                ActivePanel.Visibility = Visibility.Visible;
                FinalStatePanel.Visibility = Visibility.Collapsed;
                FooterText.Visibility = Visibility.Visible;

                FooterText.Text = "Payment complete. Thank you.";
                FooterText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 16, 185, 129)); // #10B981

                await ResetToIdleAfterDelay();
                break;

            case "PendingPayment":
                IdlePanel.Visibility = Visibility.Collapsed;
                ActivePanel.Visibility = Visibility.Visible;
                FinalStatePanel.Visibility = Visibility.Collapsed;
                FooterText.Visibility = Visibility.Visible;

                FooterText.Text = "Service marked pending payment.";
                FooterText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 245, 158, 11)); // #F59E0B

                await ResetToIdleAfterDelay();
                break;
        }
    }

    private void UpdatePaymentView(CashBoxCustomerDisplayState state)
    {
        if (state.PaymentMethod == CustomerPaymentMethod.Zelle)
        {
            CashPaymentPanel.Visibility = Visibility.Collapsed;
            ZellePaymentPanel.Visibility = Visibility.Visible;
        }
        else
        {
            CashPaymentPanel.Visibility = Visibility.Visible;
            ZellePaymentPanel.Visibility = Visibility.Collapsed;
            
            TenderedAmountText.Text = $"${state.TenderedAmount:0.00}";
            ChangeAmountText.Text = $"${state.ChangeAmount:0.00}";
        }
    }

    private async Task ResetToIdleAfterDelay()
    {
        await Task.Delay(8000);
        // Only reset if it's still in completed or pending state (user hasn't started a new ticket)
        if (CashBoxCustomerDisplayState.Current.DisplayState == "Completed" || 
            CashBoxCustomerDisplayState.Current.DisplayState == "PendingPayment")
        {
            CashBoxCustomerDisplayState.Current.SetIdle();
        }
    }

    private void Window_Closed(object sender, WindowEventArgs args)
    {
        CashBoxCustomerDisplayState.Current.StateChanged -= OnStateChanged;
    }
}
