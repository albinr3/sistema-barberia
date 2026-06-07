using Barberia.Data.Models;
using Barberia.Desktop.Services;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace Barberia.Desktop.Views;

public sealed partial class CashBoxPage : Page
{
    private const double NarrowLayoutThreshold = 900;

    private static readonly SolidColorBrush SuccessTextBrush = Brush(17, 105, 88);
    private static readonly SolidColorBrush ErrorTextBrush = Brush(154, 58, 47);
    private static readonly SolidColorBrush NeutralTextBrush = Brush(26, 28, 30);

    private readonly CashBoxCloseService _service = new();
    private readonly MediaPlayer _successPlayer;
    private IReadOnlyList<Service> _services = [];
    private decimal _additionalAmount;

    public event EventHandler? ShellMenuRequested;

    public CashBoxPage()
    {
        InitializeComponent();
        _successPlayer = new MediaPlayer();
        _successPlayer.Source = MediaSource.CreateFromUri(new Uri(System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "cashbox.mp3")));
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        ApplyResponsiveLayout(ActualWidth);
        LoadCashBox();
        ShowReadyState();
        DispatcherQueue.TryEnqueue(() => _ticketInput.Focus(FocusState.Programmatic));
    }

    private void OnCloseClick(object sender, RoutedEventArgs args)
    {
        CloseService();
    }

    private void OnMenuButtonClick(object sender, RoutedEventArgs args)
    {
        ShellMenuRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs args)
    {
        ApplyResponsiveLayout(args.NewSize.Width);
    }

    private void OnServiceSelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        _additionalAmount = 0;
        SyncAdditionalButtons(null);
        UpdateServiceTotal();
    }

    private void OnAdditionalClick(object sender, RoutedEventArgs args)
    {
        if (sender is not ToggleButton button
            || button.Tag is not string tag
            || !decimal.TryParse(tag, out var selectedAmount))
        {
            return;
        }

        _additionalAmount = button.IsChecked == true ? selectedAmount : 0;
        SyncAdditionalButtons(button);
        UpdateServiceTotal();
    }

    private void OnInputKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs args)
    {
        if (args.Key == Windows.System.VirtualKey.Enter)
        {
            LookupTicket();
            args.Handled = true;
        }
    }

    private void OnTicketInputLostFocus(object sender, RoutedEventArgs args)
    {
        if (!string.IsNullOrWhiteSpace(_ticketInput.Text))
        {
            LookupTicket();
        }
    }

    private void LoadCashBox()
    {
        try
        {
            var snapshot = _service.Load();
            _services = snapshot.Services;
            _serviceSelector.ItemsSource = _services;
            _lastRefreshText.Text = $"Updated: {snapshot.LoadedAt:hh:mm tt}";
            SetMessage("Waiting for ticket and service.", SuccessTextBrush);
            UpdateServiceTotal();
        }
        catch (Exception exception)
        {
            ShowError(exception.Message);
        }
    }

    private void CloseService()
    {
        if (_serviceSelector.SelectedItem is not Service selectedService)
        {
            ShowError("Select a service.");
            DispatcherQueue.TryEnqueue(() => _ticketInput.Focus(FocusState.Programmatic));
            return;
        }

        try
        {
            var result = _service.CloseService(_ticketInput.Text, selectedService.Id, _additionalAmount);

            _serviceReceiptText.Text = result.AdditionalAmount > 0
                ? $"{result.ServiceName} {result.ServicePrice:0.00} + addition {result.AdditionalAmount:0.00}"
                : $"{result.ServiceName} {result.ServicePrice:0.00}";
            _successPlayer.Play();
            SetMessage($"{result.DisplayTicketNumber} - {result.BarberStationCode} - {result.Message}", SuccessTextBrush);
            _ticketInput.Text = string.Empty;
            ClearTicketDetails();
            _serviceSelector.SelectedItem = null;
            _additionalAmount = 0;
            SyncAdditionalButtons(null);
            UpdateServiceTotal();
            LoadCashBox();
        }
        catch (Exception exception)
        {
            ShowError(exception.Message);
        }
        finally
        {
            DispatcherQueue.TryEnqueue(() => _ticketInput.Focus(FocusState.Programmatic));
        }
    }

    private void ShowReadyState()
    {
        _amountText.Text = "$0.00";
        _commissionText.Text = "$0.00";
        _serviceReceiptText.Text = "No service";
        _servicePriceText.Text = string.Empty;
        _cashTotalText.Text = "$0.00";
        ClearTicketDetails();
        SetMessage("Waiting for ticket and service.", NeutralTextBrush);
    }

    private void LookupTicket()
    {
        try
        {
            var ticket = _service.LookupTicket(_ticketInput.Text);
            _ticketCustomerText.Text = ticket.CustomerName;
            _ticketBarberText.Text = $"{ticket.BarberStationCode} - {ticket.BarberName}";
            SetMessage($"Ticket {ticket.DisplayTicketNumber} found. Verify customer and barber before completing.", SuccessTextBrush);
        }
        catch (Exception exception)
        {
            ClearTicketDetails();
            ShowError(exception.Message);
        }
    }

    private void ClearTicketDetails()
    {
        _ticketCustomerText.Text = "No ticket";
        _ticketBarberText.Text = "No ticket";
    }

    private void UpdateServiceTotal()
    {
        if (_serviceSelector.SelectedItem is not Service selectedService)
        {
            _servicePriceText.Text = string.Empty;
            _amountText.Text = "$0.00";
            _commissionText.Text = "$0.00";
            _cashTotalText.Text = "$0.00";
            return;
        }
        _servicePriceText.Text = $"${selectedService.Price:0.00}";
        _amountText.Text = $"${selectedService.Price:0.00}";
        _commissionText.Text = $"${_additionalAmount:0.00}";
        _cashTotalText.Text = $"${selectedService.Price + _additionalAmount:0.00}";
    }

    private void SyncAdditionalButtons(ToggleButton? selectedButton)
    {
        foreach (var button in new[] { _additional2Button, _additional3Button, _additional5Button })
        {
            if (button != selectedButton)
            {
                button.IsChecked = false;
            }
        }
    }

    private void ShowError(string message)
    {
        SetMessage(message, ErrorTextBrush);
    }

    private void ApplyResponsiveLayout(double width)
    {
        var useNarrowLayout = width > 0 && width < NarrowLayoutThreshold;

        _screenScrollViewer.Padding = useNarrowLayout
            ? new Thickness(20, 72, 20, 24)
            : new Thickness(48, 48, 48, 32);

        _cashBoxContentGrid.ColumnSpacing = useNarrowLayout ? 0 : 24;
        _cashBoxContentGrid.RowSpacing = useNarrowLayout ? 24 : 0;
        _activeTicketColumn.Width = new GridLength(7, GridUnitType.Star);
        _paymentSummaryColumn.Width = useNarrowLayout
            ? new GridLength(0)
            : new GridLength(3, GridUnitType.Star);
        _paymentSummaryColumn.MinWidth = useNarrowLayout ? 0 : 320;

        Grid.SetColumn(_summaryPanel, useNarrowLayout ? 0 : 1);
        Grid.SetRow(_summaryPanel, useNarrowLayout ? 1 : 0);
    }

    private void SetMessage(string message, SolidColorBrush foreground)
    {
        _messageText.Text = message;
        _messageText.Foreground = foreground;
    }

    private static SolidColorBrush Brush(byte red, byte green, byte blue)
    {
        return new SolidColorBrush(ColorHelper.FromArgb(255, red, green, blue));
    }
}
