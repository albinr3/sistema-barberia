using Barberia.Core.Domain;
using Barberia.Desktop.Services;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Barberia.Desktop.Views;

public sealed partial class CashBoxPage : Page
{
    private readonly CashBoxCloseService _service = new();
    private IReadOnlyList<Barber> _barbers = [];

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
            _barberSelector.ItemsSource = _barbers;
            _lastRefreshText.Text = $"Actualizado: {snapshot.LoadedAt:hh:mm tt}";
            SetStatus("Local", success: true);
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

        try
        {
            var result = _service.CloseService(barber.Id, _ticketInput.Text, _amountInput.Text);
            _receiptText.Text = result.ReceiptNumber;
            _amountText.Text = $"{result.Amount:0.00}";
            _commissionText.Text = $"{result.Commission:0.00}";
            _messageText.Text = $"{result.TicketNumber} - {result.BarberStationCode} - {result.Message}";
            _ticketInput.Text = string.Empty;
            _amountInput.Text = string.Empty;
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
        _messageText.Text = "Esperando ticket, barbero y monto cobrado.";
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
