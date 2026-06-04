using Barberia.Desktop.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Barberia.Desktop.Views;

public sealed partial class KioskPage : Page
{
    private readonly KioskCheckInService _checkInService = new();

    public KioskPage()
    {
        InitializeComponent();
        ShowReadyState();
    }

    private void OnCheckInButtonClick(object sender, RoutedEventArgs args)
    {
        _checkInButton.IsEnabled = false;
        _statusBadgeText.Text = "Registrando";

        try
        {
            ShowResult(_checkInService.RegisterWalkIn());
        }
        catch (Exception exception)
        {
            ShowError(exception.Message);
        }
        finally
        {
            _checkInButton.IsEnabled = true;
        }
    }

    private void ShowReadyState()
    {
        _statusBadgeText.Text = "Listo";
        _ticketNumber.Text = "Sin ticket";
        _assignmentText.Text = "Esperando registro";
        _messageText.Text = "La proxima llegada generara un ticket local.";
        _timeText.Text = string.Empty;
    }

    private void ShowResult(KioskCheckInResult result)
    {
        _ticketNumber.Text = result.TicketNumber;
        _assignmentText.Text = result.Status == KioskCheckInStatus.Assigned
            ? $"Barbero asignado: {result.AssignedBarberName}"
            : "Turno en espera";
        _messageText.Text = result.Message;
        _timeText.Text = $"Registrado: {result.CheckedInAt:hh:mm tt}";
        _statusBadgeText.Text = result.Status == KioskCheckInStatus.Assigned ? "Asignado" : "En espera";
    }

    private void ShowError(string message)
    {
        _ticketNumber.Text = "No registrado";
        _assignmentText.Text = "Revisar operacion local";
        _messageText.Text = message;
        _timeText.Text = string.Empty;
        _statusBadgeText.Text = "Error";
    }
}
