using Barberia.Desktop.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Runtime.InteropServices;

namespace Barberia.Desktop.Views;

public sealed partial class BarberPanelPage : Page
{
    private const uint ErrorBeepType = 0xFFFFFFFF;
    private readonly BarberPanelService _service = new();

    public event EventHandler? ShellMenuRequested;

    public BarberPanelPage()
    {
        InitializeComponent();
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        _ticketInput.Focus(FocusState.Programmatic);
    }

    private void OnMenuButtonClick(object sender, RoutedEventArgs args)
    {
        ShellMenuRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnStartServiceClick(object sender, RoutedEventArgs args)
    {
        StartService();
    }

    private void OnTicketInputKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs args)
    {
        if (args.Key == Windows.System.VirtualKey.Enter)
        {
            StartService();
            args.Handled = true;
        }
    }

    private void StartService()
    {
        try
        {
            var result = _service.StartService(_ticketInput.Text);
            _ticketInput.Text = string.Empty;
            _assignedBarberText.Text = $"{result.BarberStationCode} - {result.BarberName}";
            SetSuccessMessage($"Ticket {result.DisplayTicketNumber} started. Payment and closeout remain in Cash Box.");
            _ticketInput.Focus(FocusState.Programmatic);
        }
        catch (Exception exception)
        {
            _assignedBarberText.Text = "Review ticket";
            SetErrorMessage(exception.Message);
        }
    }

    private void SetSuccessMessage(string message)
    {
        _messageText.Text = message;
        _messageText.Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 101, 108, 116));
        _messageText.FontSize = 14;
    }

    private void SetErrorMessage(string message)
    {
        MessageBeep(ErrorBeepType);
        _messageText.Text = $"ERROR: {message}";
        _messageText.Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 190, 35, 35));
        _messageText.FontSize = 20;
    }

    [DllImport("user32.dll")]
    private static extern bool MessageBeep(uint uType);
}
