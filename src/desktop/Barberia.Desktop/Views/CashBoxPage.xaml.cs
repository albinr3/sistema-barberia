using Barberia.Core.Domain;
using Barberia.Data.Models;
using Barberia.Desktop.Services;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;

namespace Barberia.Desktop.Views;

public sealed partial class CashBoxPage : Page
{
    private readonly CashBoxCloseService _service = new();
    private IReadOnlyList<Barber> _barbers = [];
    private IReadOnlyList<Service> _services = [];
    private decimal _additionalAmount;

    public CashBoxPage()
    {
        InitializeComponent();
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        LoadCashBox();
        ShowReadyState();
    }

    private void OnCloseClick(object sender, RoutedEventArgs args)
    {
        CloseService();
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
            CloseService();
            args.Handled = true;
        }
    }

    private void LoadCashBox()
    {
        try
        {
            var snapshot = _service.Load();
            _barbers = snapshot.Barbers;
            _services = snapshot.Services;
            _barberSelector.ItemsSource = _barbers;
            _serviceSelector.ItemsSource = _services;
            _lastRefreshText.Text = $"Actualizado: {snapshot.LoadedAt:hh:mm tt}";
            SetStatus("Local", success: true);
            UpdateServiceTotal();
        }
        catch (Exception exception)
        {
            ShowError(exception.Message);
        }
    }

    private void CloseService()
    {
        if (_barberSelector.SelectedItem is not Barber barber)
        {
            ShowError("Selecciona un barbero.");
            return;
        }

        if (_serviceSelector.SelectedItem is not Service selectedService)
        {
            ShowError("Selecciona un servicio.");
            return;
        }

        try
        {
            var result = _service.CloseService(barber.Id, _ticketInput.Text, selectedService.Id, _additionalAmount);
            _receiptText.Text = result.ReceiptNumber;
            _amountText.Text = $"{result.Amount:0.00}";
            _commissionText.Text = $"{result.Commission:0.00}";
            _serviceReceiptText.Text = result.AdditionalAmount > 0
                ? $"{result.ServiceName} {result.ServicePrice:0.00} + adicional {result.AdditionalAmount:0.00}"
                : $"{result.ServiceName} {result.ServicePrice:0.00}";
            _messageText.Text = $"{result.DisplayTicketNumber} - {result.BarberStationCode} - {result.Message}";
            _ticketInput.Text = string.Empty;
            _serviceSelector.SelectedItem = null;
            _additionalAmount = 0;
            SyncAdditionalButtons(null);
            UpdateServiceTotal();
            SetStatus("Cerrado", success: true);
            LoadCashBox();
            _barberSelector.SelectedItem = _barbers.FirstOrDefault(candidate => candidate.Id == barber.Id);
        }
        catch (Exception exception)
        {
            ShowError(exception.Message);
        }
    }

    private void ShowReadyState()
    {
        _receiptText.Text = "Sin recibo";
        _amountText.Text = "0.00";
        _commissionText.Text = "0.00";
        _serviceReceiptText.Text = "Sin servicio";
        _servicePriceText.Text = "Selecciona un servicio para cargar el precio base.";
        _cashTotalText.Text = "Total: $0.00";
        _messageText.Text = "Esperando ticket, barbero y servicio.";
    }

    private void UpdateServiceTotal()
    {
        if (_serviceSelector.SelectedItem is not Service selectedService)
        {
            _servicePriceText.Text = "Selecciona un servicio para cargar el precio base.";
            _cashTotalText.Text = "Total: $0.00";
            return;
        }

        _servicePriceText.Text = _additionalAmount > 0
            ? $"Precio base: ${selectedService.Price:0.00}. Adicional seleccionado: ${_additionalAmount:0.00}."
            : $"Precio base: ${selectedService.Price:0.00}. Sin adicional.";
        _cashTotalText.Text = $"Total: ${selectedService.Price + _additionalAmount:0.00}";
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
        _messageText.Text = message;
        SetStatus("Revisar", success: false);
    }

    private void SetStatus(string text, bool success)
    {
        _statusBadgeText.Text = text;
        _statusBadge.Background = success ? Brush(235, 248, 244) : Brush(255, 240, 238);
        _statusBadge.BorderBrush = success ? Brush(181, 224, 211) : Brush(231, 170, 162);
        _statusBadgeText.Foreground = success ? Brush(17, 105, 88) : Brush(154, 58, 47);
    }

    private static SolidColorBrush Brush(byte red, byte green, byte blue)
    {
        return new SolidColorBrush(ColorHelper.FromArgb(255, red, green, blue));
    }
}
