using Barberia.Desktop.Services;
using Microsoft.UI.Xaml;

namespace Barberia.Desktop.Views;

public sealed partial class ConnectionBlockedWindow : Window
{
    internal ConnectionBlockedWindow(StationSettings settings, LanHealthCheckResult health)
    {
        InitializeComponent();
        AppWindow.Title = "Barberia Station Offline";
        _roleText.Text = $"Station: {settings.Role} | Server: {settings.LanServerUrl}";
        _messageText.Text = health.Message;
    }
}

