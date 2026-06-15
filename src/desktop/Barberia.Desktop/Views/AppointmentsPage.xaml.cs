using Barberia.Core.Domain;
using Barberia.Desktop.Services;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Barberia.Desktop.Views;

public sealed partial class AppointmentsPage : Page
{
    private readonly LocalAppointmentsService _service = new();

    public event EventHandler? ShellMenuRequested;

    public AppointmentsPage()
    {
        InitializeComponent();
    }

    private void OnMenuButtonClick(object sender, RoutedEventArgs args)
    {
        ShellMenuRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        LoadAppointments();
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
            ColumnSpacing = 18,
            Padding = new Thickness(18, 16, 18, 16)
        };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(112) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });

        var timePanel = new StackPanel
        {
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Center
        };
        timePanel.Children.Add(new TextBlock
        {
            Text = item.Appointment.ScheduledFor.ToString("hh:mm tt"),
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 26, 28, 30)),
            TextWrapping = TextWrapping.NoWrap
        });
        timePanel.Children.Add(new TextBlock
        {
            Text = item.Appointment.ScheduledFor.ToString("MMM d"),
            FontSize = 12,
            Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 101, 108, 116)),
            TextWrapping = TextWrapping.NoWrap
        });
        row.Children.Add(timePanel);

        var clientPanel = new StackPanel
        {
            Spacing = 4,
            VerticalAlignment = VerticalAlignment.Center
        };
        clientPanel.Children.Add(new TextBlock
        {
            Text = item.Appointment.CustomerName,
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 26, 28, 30)),
            TextWrapping = TextWrapping.WrapWholeWords
        });
        clientPanel.Children.Add(new TextBlock
        {
            Text = item.Service?.Name ?? "-",
            FontSize = 14,
            Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 68, 70, 85)),
            TextWrapping = TextWrapping.WrapWholeWords
        });
        Grid.SetColumn(clientPanel, 1);
        row.Children.Add(clientPanel);

        var assignmentPanel = new StackPanel
        {
            Spacing = 4,
            VerticalAlignment = VerticalAlignment.Center
        };
        assignmentPanel.Children.Add(new TextBlock
        {
            Text = "Barber:",
            FontSize = 14,
            Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 101, 108, 116))
        });
        assignmentPanel.Children.Add(new TextBlock
        {
            Text = item.Barber?.DisplayNameWithStation ?? "Any barber",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 26, 28, 30)),
            TextWrapping = TextWrapping.WrapWholeWords
        });
        Grid.SetColumn(assignmentPanel, 2);
        row.Children.Add(assignmentPanel);

        var statusPanel = new StackPanel
        {
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        statusPanel.Children.Add(CreateStatusBadge(item));
        statusPanel.Children.Add(new TextBlock
        {
            Text = IsActiveState(item) ? "Operational" : "Closed",
            FontSize = 12,
            Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 101, 108, 116)),
            HorizontalAlignment = HorizontalAlignment.Right
        });
        Grid.SetColumn(statusPanel, 3);
        row.Children.Add(statusPanel);

        return new Border
        {
            Background = new SolidColorBrush(IsActiveState(item)
                ? ColorHelper.FromArgb(255, 255, 255, 255)
                : ColorHelper.FromArgb(255, 248, 249, 251)),
            BorderBrush = new SolidColorBrush(IsActiveState(item)
                ? ColorHelper.FromArgb(255, 197, 207, 221)
                : ColorHelper.FromArgb(255, 226, 230, 235)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
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
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 6, 10, 6),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = new TextBlock
            {
                Text = text,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
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
            Padding = new Thickness(24, 48, 24, 48),
            Child = new TextBlock
            {
                Text = text,
                FontSize = 14,
                Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 101, 108, 116)),
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Center
            }
        };
    }
}
