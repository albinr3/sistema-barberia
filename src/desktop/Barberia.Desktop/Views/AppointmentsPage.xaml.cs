using Barberia.Core.Domain;
using Barberia.Desktop.Services;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System.Runtime.InteropServices;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace Barberia.Desktop.Views;

public sealed partial class AppointmentsPage : Page
{
    private const uint ErrorBeepType = 0xFFFFFFFF;
    private readonly LocalAppointmentsService _service = new();
    private readonly BarberPanelService _startService = new();
    private readonly DispatcherTimer _refreshTimer = new();
    private readonly MediaPlayer _successPlayer = new();

    public event EventHandler? ShellMenuRequested;

    public AppointmentsPage()
    {
        InitializeComponent();
        _refreshTimer.Interval = TimeSpan.FromSeconds(15);
        _refreshTimer.Tick += (_, _) => LoadAppointments();
        _successPlayer.Source = MediaSource.CreateFromUri(new Uri(System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "barberpanel.wav")));
    }

    private void OnMenuButtonClick(object sender, RoutedEventArgs args)
    {
        ShellMenuRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        LoadAppointments();
        _refreshTimer.Start();
        QueueScanFocus();
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        _refreshTimer.Stop();
    }

    private void LoadAppointments()
    {
        try
        {
            var snapshot = _service.Load();
            ShowSnapshot(snapshot);
        }
        catch (Exception exception)
        {
            _appointmentsRows.Children.Clear();
            _appointmentsRows.Children.Add(CreateEmptyState($"Could not load appointments: {exception.Message}"));
        }
        finally
        {
            QueueScanFocus();
        }
    }

    private void OnPageRootTapped(object sender, TappedRoutedEventArgs args)
    {
        QueueScanFocus();
    }

    private void OnScanInputKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs args)
    {
        if (args.Key == Windows.System.VirtualKey.Enter)
        {
            StartScannedService();
            args.Handled = true;
        }
    }

    private void StartScannedService()
    {
        try
        {
            var result = _startService.StartService(_scanInput.Text);
            _scanInput.Text = string.Empty;
            SetScanSuccess($"Started #{result.DisplayTicketNumber} - {result.BarberStationCode}");
            LoadAppointments();
        }
        catch (Exception exception)
        {
            _scanInput.Text = string.Empty;
            SetScanError(exception.Message);
        }
        finally
        {
            QueueScanFocus();
        }
    }

    private void QueueScanFocus()
    {
        DispatcherQueue.TryEnqueue(() => _scanInput.Focus(FocusState.Programmatic));
    }

    private void SetScanSuccess(string message)
    {
        _successPlayer.Play();
        _scanMessageText.Text = message;
        _scanMessageText.Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 17, 105, 88));
    }

    private void SetScanError(string message)
    {
        MessageBeep(ErrorBeepType);
        _scanMessageText.Text = message;
        _scanMessageText.Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 190, 35, 35));
    }

    private void ShowSnapshot(AppointmentsSnapshot snapshot)
    {
        _appointmentsRows.Children.Clear();

        if (snapshot.Items.Count == 0)
        {
            _appointmentsRows.Children.Add(CreateEmptyState("No appointments scheduled for today."));
            return;
        }

        var orderedItems = snapshot.Items
            .OrderBy(item => IsActiveState(item) ? 0 : 1)
            .ThenBy(item => item.Appointment.ScheduledFor)
            .ThenBy(item => item.Appointment.AppointmentCode ?? string.Empty)
            .ThenBy(item => item.Appointment.Id)
            .ToList();

        foreach (var item in orderedItems)
        {
            _appointmentsRows.Children.Add(CreateAppointmentRow(item));
        }
    }

    private static bool IsActiveState(AppointmentSnapshotItem item)
    {
        if (item.LocalTurn?.State is TurnState.Completed or TurnState.Cancelled or TurnState.NoShow or TurnState.Voided)
        {
            return false;
        }

        return item.Appointment.State is AppointmentState.Confirmed or AppointmentState.ProtectionStarted or AppointmentState.CheckedIn;
    }

    private static UIElement CreateAppointmentRow(AppointmentSnapshotItem item)
    {
        var row = new Grid
        {
            ColumnSpacing = 24,
            Padding = new Thickness(24, 28, 24, 28)
        };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) });

        var timePanel = new StackPanel
        {
            Spacing = 4,
            VerticalAlignment = VerticalAlignment.Center
        };
        timePanel.Children.Add(new TextBlock
        {
            Text = item.Appointment.ScheduledFor.ToString("hh:mm tt"),
            FontSize = 36,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 26, 28, 30)),
            TextWrapping = TextWrapping.NoWrap
        });
        timePanel.Children.Add(new TextBlock
        {
            Text = item.Appointment.ScheduledFor.ToString("MMM d"),
            FontSize = 20,
            Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 101, 108, 116)),
            TextWrapping = TextWrapping.NoWrap
        });
        row.Children.Add(timePanel);

        var detailsPanel = new StackPanel
        {
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center
        };
        detailsPanel.Children.Add(new TextBlock
        {
            Text = item.Appointment.CustomerName,
            FontSize = 32,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 26, 28, 30)),
            TextWrapping = TextWrapping.WrapWholeWords
        });
        
        var serviceBarberText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        };
        
        serviceBarberText.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run
        {
            Text = item.Service?.Name ?? "General Service",
            FontSize = 22,
            Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 68, 70, 85))
        });
        
        serviceBarberText.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run
        {
            Text = "  •  ",
            FontSize = 22,
            Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 197, 207, 221))
        });
        
        serviceBarberText.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run
        {
            Text = item.Barber?.DisplayNameWithStation ?? "Any barber",
            FontSize = 26,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 0, 19, 135))
        });
        
        detailsPanel.Children.Add(serviceBarberText);
        
        Grid.SetColumn(detailsPanel, 1);
        row.Children.Add(detailsPanel);

        var statusPanel = new StackPanel
        {
            Spacing = 12,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        statusPanel.Children.Add(CreateStatusBadge(item));
        statusPanel.Children.Add(new TextBlock
        {
            Text = IsActiveState(item) ? "Operational" : "Closed",
            FontSize = 18,
            Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 101, 108, 116)),
            HorizontalAlignment = HorizontalAlignment.Right
        });
        Grid.SetColumn(statusPanel, 2);
        row.Children.Add(statusPanel);

        return new Border
        {
            Background = new SolidColorBrush(IsActiveState(item)
                ? ColorHelper.FromArgb(255, 255, 255, 255)
                : ColorHelper.FromArgb(255, 248, 249, 251)),
            BorderBrush = new SolidColorBrush(IsActiveState(item)
                ? ColorHelper.FromArgb(255, 197, 207, 221)
                : ColorHelper.FromArgb(255, 226, 230, 235)),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(12),
            Child = row
        };
    }

    private static TextBlock CreateMetaText(string label, string value)
    {
        return new TextBlock
        {
            Text = $"{label}: {value}",
            FontSize = 14,
            Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 68, 70, 85)),
            TextWrapping = TextWrapping.WrapWholeWords
        };
    }

    private static FrameworkElement CreateStatusBadge(AppointmentSnapshotItem item)
    {
        string text;
        Windows.UI.Color bg;
        Windows.UI.Color fg;

        if (item.LocalTurn is not null)
        {
            text = item.LocalTurn.State switch
            {
                TurnState.Waiting => "Waiting locally",
                TurnState.Called => "Called",
                TurnState.InService => "In service",
                TurnState.Completed => "Completed",
                TurnState.Cancelled => "Cancelled",
                TurnState.NoShow => "No show",
                TurnState.Voided => "Voided",
                _ => item.LocalTurn.State.ToString()
            };

            (bg, fg) = item.LocalTurn.State switch
            {
                TurnState.Waiting => (ColorHelper.FromArgb(255, 232, 232, 234), ColorHelper.FromArgb(255, 26, 28, 30)),
                TurnState.Called => (ColorHelper.FromArgb(255, 223, 224, 255), ColorHelper.FromArgb(255, 0, 19, 135)),
                TurnState.InService => (ColorHelper.FromArgb(255, 255, 218, 214), ColorHelper.FromArgb(255, 147, 0, 10)),
                TurnState.Completed => (ColorHelper.FromArgb(255, 220, 252, 231), ColorHelper.FromArgb(255, 22, 101, 52)),
                _ => (ColorHelper.FromArgb(255, 248, 249, 251), ColorHelper.FromArgb(255, 101, 108, 116))
            };
        }
        else
        {
            text = item.Appointment.State switch
            {
                AppointmentState.Confirmed => "Upcoming",
                AppointmentState.ProtectionStarted => "Reserved",
                AppointmentState.CheckedIn => "Checked in",
                AppointmentState.Completed => "Completed",
                AppointmentState.NoShow => "No show",
                AppointmentState.Cancelled => "Cancelled",
                _ => item.Appointment.State.ToString()
            };

            (bg, fg) = item.Appointment.State switch
            {
                AppointmentState.Confirmed => (ColorHelper.FromArgb(255, 235, 248, 244), ColorHelper.FromArgb(255, 17, 105, 88)),
                AppointmentState.ProtectionStarted => (ColorHelper.FromArgb(255, 255, 248, 230), ColorHelper.FromArgb(255, 140, 96, 16)),
                AppointmentState.CheckedIn => (ColorHelper.FromArgb(255, 223, 224, 255), ColorHelper.FromArgb(255, 0, 19, 135)),
                AppointmentState.Completed => (ColorHelper.FromArgb(255, 220, 252, 231), ColorHelper.FromArgb(255, 22, 101, 52)),
                _ => (ColorHelper.FromArgb(255, 248, 249, 251), ColorHelper.FromArgb(255, 101, 108, 116))
            };
        }

        return new Border
        {
            Background = new SolidColorBrush(bg),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(20, 10, 20, 10),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = new TextBlock
            {
                Text = text,
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(fg)
            }
        };
    }

    private static UIElement CreateEmptyState(string text)
    {
        return new Border
        {
            Background = new SolidColorBrush(ColorHelper.FromArgb(255, 255, 255, 255)),
            BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(255, 238, 238, 240)),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(32, 64, 32, 64),
            Child = new TextBlock
            {
                Text = text,
                FontSize = 24,
                Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 101, 108, 116)),
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Center
            }
        };
    }

    [DllImport("user32.dll")]
    private static extern bool MessageBeep(uint uType);
}
