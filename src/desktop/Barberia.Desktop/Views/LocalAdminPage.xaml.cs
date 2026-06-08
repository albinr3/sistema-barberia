using Barberia.Core.Domain;
using Barberia.Data.Models;
using Barberia.Desktop.Services;
using Barberia.Desktop.Shell;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Barberia.Desktop.Views;

public sealed partial class LocalAdminPage : Page
{
    private readonly LocalAdminService _service = new();

    public LocalAdminPage()
    {
        InitializeComponent();
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        LoadAdmin();
    }

    private void OnRefreshClick(object sender, RoutedEventArgs args)
    {
        LoadAdmin();
    }

    private void LoadAdmin()
    {
        try
        {
            var snapshot = _service.Load();
            ShowSnapshot(snapshot);
            SetStatus("Local", success: true);
        }
        catch (Exception exception)
        {
            ShowError(exception.Message);
        }
    }

    private void ShowSnapshot(LocalAdminSnapshot snapshot)
    {
        var activeCount = snapshot.Barbers.Count(barber => barber.IsActive);
        var availableCount = snapshot.Barbers.Count(barber => barber.IsActive && barber.State == BarberState.Available);

        _activeTurnsText.Text = snapshot.ActiveTurns.Count.ToString();
        _checkInsText.Text = snapshot.Operations.CheckIns.ToString();
        _availableBarbersText.Text = $"{availableCount}/{activeCount}";
        _cashText.Text = FormatMoney(snapshot.Cash.TotalAmountCents, snapshot.Cash.Currency);
        _lastRefreshText.Text = $"Updated: {snapshot.GeneratedAt:hh:mm tt}";
        _databasePathText.Text = snapshot.DatabasePath;
        _databaseSizeText.Text = FormatBytes(snapshot.DatabaseSizeBytes);
        _messageText.Text = $"Completed services today: {snapshot.Operations.CompletedServices}. Cash payments: {snapshot.Cash.PaymentCount}.";
        UpdateReassignmentControls(snapshot);
        ReplaceChildren(
            _alertRows,
            snapshot.Alerts.Select(CreateAlertRow),
            "No current alerts.");
        ReplaceChildren(
            _turnRows,
            snapshot.ActiveTurns.Select(turn => CreateTurnRow(turn, snapshot.Barbers)),
            "No active turns right now.");
        ReplaceChildren(
            _auditRows,
            snapshot.RecentAuditEvents.Select(CreateAuditRow),
            "No audit events recorded yet.");
    }


    private void UpdateReassignmentControls(LocalAdminSnapshot snapshot)
    {
        var ticketOptions = snapshot.ActiveTurns
            .Where(turn => turn.State is TurnState.Waiting or TurnState.Called)
            .Select(turn => new ReassignTicketOption(turn.Id, FormatReassignTicketOption(turn, snapshot.Barbers)))
            .ToArray();
        var barberOptions = snapshot.Barbers
            .Where(barber => barber.IsActive)
            .Select(barber => new ReassignBarberOption(barber.Id, FormatReassignBarberOption(barber)))
            .ToArray();

        _reassignTicketComboBox.ItemsSource = ticketOptions;
        _reassignTicketComboBox.SelectedIndex = ticketOptions.Length > 0 ? 0 : -1;
        _reassignTicketComboBox.IsEnabled = ticketOptions.Length > 0;

        _reassignBarberComboBox.ItemsSource = barberOptions;
        _reassignBarberComboBox.SelectedIndex = barberOptions.Length > 0 ? 0 : -1;
        _reassignBarberComboBox.IsEnabled = barberOptions.Length > 0;

        UpdateReassignmentButtonState();
    }


    private UIElement CreateTurnRow(Turn turn, IReadOnlyList<Barber> barbers)
    {
        var barberName = FormatTurnBarberText(turn, barbers);
        var customerName = string.IsNullOrWhiteSpace(turn.CustomerName) ? "Walk-in customer" : turn.CustomerName;

        var row = new Grid
        {
            ColumnSpacing = 12,
            Children =
            {
                new StackPanel
                {
                    Spacing = 3,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = $"{turn.DisplayTicketNumber} - {customerName}",
                            FontSize = 17,
                            FontWeight = FontWeights.SemiBold,
                            Foreground = Brush(30, 31, 34),
                            TextWrapping = TextWrapping.WrapWholeWords
                        },
                        new TextBlock
                        {
                            Text = $"{FormatTurnSource(turn.Source)} - {barberName} - {turn.CheckedInAt:hh:mm tt} - Interno {turn.TicketNumber}",
                            FontSize = 13,
                            Foreground = Brush(101, 108, 116),
                            TextWrapping = TextWrapping.WrapWholeWords
                        }
                    }
                }
            }
        };
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        actions.Children.Add(CreateTextBadge(
            FormatTurnState(turn.State),
            GetTurnBackground(turn.State),
            GetTurnForeground(turn.State)));

        var cancelButton = CreateSmallActionButton("Cancel");
        cancelButton.IsEnabled = turn.State is TurnState.Waiting or TurnState.Called or TurnState.InService;
        cancelButton.Click += (_, _) => ExecuteAdminAction(() => _service.CancelTurn(turn.Id), "Cancelled");
        actions.Children.Add(cancelButton);

        Grid.SetColumn(actions, 1);
        row.Children.Add(actions);

        return WrapRow(row, Brush(255, 255, 255));
    }

    private void OnReassignmentSelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        UpdateReassignmentButtonState();
    }

    private void OnReassignTicketClick(object sender, RoutedEventArgs args)
    {
        if (_reassignTicketComboBox.SelectedItem is not ReassignTicketOption ticketOption)
        {
            _messageText.Text = "Select a waiting or called ticket before reassigning.";
            SetStatus("Action blocked", success: false);
            return;
        }

        if (_reassignBarberComboBox.SelectedItem is not ReassignBarberOption barberOption)
        {
            _messageText.Text = "Select an active barber before reassigning.";
            SetStatus("Action blocked", success: false);
            return;
        }

        ExecuteAdminAction(
            () => _service.ReassignTurn(ticketOption.TurnId, barberOption.BarberId),
            "Reassigned");
    }

    private void UpdateReassignmentButtonState()
    {
        _reassignButton.IsEnabled =
            _reassignTicketComboBox.SelectedItem is ReassignTicketOption
            && _reassignBarberComboBox.SelectedItem is ReassignBarberOption;
    }

    private static UIElement CreateAlertRow(LocalAdminAlert alert)
    {
        var row = new Grid { ColumnSpacing = 12 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition());

        var iconBox = new Border
        {
            Width = 42,
            Height = 42,
            CornerRadius = new CornerRadius(8),
            Background = alert.Severity switch
            {
                AlertSeverity.Critical => Brush(255, 240, 238),
                AlertSeverity.Warning => Brush(255, 248, 230),
                _ => Brush(235, 248, 244)
            }
        };

        var icon = new FontIcon
        {
            Glyph = alert.Severity switch
            {
                AlertSeverity.Critical => "\uEA39", // Warning icon
                AlertSeverity.Warning => "\uE7BA", // Warning icon
                _ => "\uE946" // Info icon
            },
            FontSize = 19,
            Foreground = alert.Severity switch
            {
                AlertSeverity.Critical => Brush(154, 58, 47),
                AlertSeverity.Warning => Brush(140, 96, 16),
                _ => Brush(17, 105, 88)
            },
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        iconBox.Child = icon;
        Grid.SetColumn(iconBox, 0);
        row.Children.Add(iconBox);

        var details = new StackPanel
        {
            Spacing = 3,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock
                {
                    Text = alert.Title,
                    FontSize = 16,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brush(30, 31, 34),
                    TextWrapping = TextWrapping.WrapWholeWords
                },
                new TextBlock
                {
                    Text = alert.Detail,
                    FontSize = 13,
                    Foreground = Brush(101, 108, 116),
                    TextWrapping = TextWrapping.WrapWholeWords
                }
            }
        };
        Grid.SetColumn(details, 1);
        row.Children.Add(details);

        return WrapRow(row, alert.Severity switch
        {
            AlertSeverity.Critical => Brush(255, 240, 238),
            AlertSeverity.Warning => Brush(255, 252, 240),
            _ => Brush(255, 255, 255)
        });
    }

    private void OnViewFullHistoryClick(object sender, RoutedEventArgs args)
    {
        if (App.MainWindowInstance is MainWindow mainWindow)
        {
            mainWindow.NavigateTo(ShellModuleKey.TicketHistory);
        }
    }

    private static UIElement CreateAuditRow(AuditEvent auditEvent)
    {
        return WrapRow(
            new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    new TextBlock
                    {
                        Text = auditEvent.EventType,
                        FontSize = 16,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = Brush(30, 31, 34),
                        TextWrapping = TextWrapping.WrapWholeWords
                    },
                    new TextBlock
                    {
                        Text = $"{auditEvent.OccurredAt:hh:mm tt} - {auditEvent.AggregateType} - {auditEvent.DeviceId ?? "local device"}",
                        FontSize = 13,
                        Foreground = Brush(101, 108, 116),
                        TextWrapping = TextWrapping.WrapWholeWords
                    }
                }
            },
            Brush(255, 255, 255));
    }

    private void ExecuteAdminAction(Action action, string successStatus = "Updated")
    {
        try
        {
            action();
            LoadAdmin();
            SetStatus(successStatus, success: true);
        }
        catch (Exception exception)
        {
            _messageText.Text = exception.Message;
            SetStatus("Action blocked", success: false);
        }
    }

    private void ShowError(string message)
    {
        _activeTurnsText.Text = "0";
        _checkInsText.Text = "0";
        _availableBarbersText.Text = "0/0";
        _cashText.Text = "USD 0.00";
        _lastRefreshText.Text = "No local snapshot loaded";
        _databasePathText.Text = LocalAppPaths.DatabasePath;
        _databaseSizeText.Text = "0 B";
        _messageText.Text = message;
        _reassignTicketComboBox.ItemsSource = null;
        _reassignTicketComboBox.IsEnabled = false;
        _reassignBarberComboBox.ItemsSource = null;
        _reassignBarberComboBox.IsEnabled = false;
        _reassignButton.IsEnabled = false;
        _turnRows.Children.Clear();
        _auditRows.Children.Clear();
        _historyRows.Children.Clear();
        _alertRows.Children.Clear();
        _alertRows.Children.Add(CreateEmptyState("Could not read alerts."));
        _turnRows.Children.Add(CreateEmptyState("Could not read active turns."));
        _auditRows.Children.Add(CreateEmptyState(message));
        SetStatus("Error", success: false);
    }

    private void SetStatus(string text, bool success)
    {
        _statusBadgeText.Text = text;
        _statusBadge.Background = success ? Brush(235, 248, 244) : Brush(255, 240, 238);
        _statusBadge.BorderBrush = success ? Brush(181, 224, 211) : Brush(231, 170, 162);
        _statusBadgeText.Foreground = success ? Brush(17, 105, 88) : Brush(154, 58, 47);
    }

    private static Button CreateSmallActionButton(string text)
    {
        return new Button
        {
            Content = text,
            MinHeight = 36,
            MinWidth = 96,
            Padding = new Thickness(12, 6, 12, 6),
            VerticalAlignment = VerticalAlignment.Center
        };
    }



    private static void ReplaceChildren(StackPanel panel, IEnumerable<UIElement> children, string emptyText)
    {
        panel.Children.Clear();
        var added = false;

        foreach (var child in children)
        {
            panel.Children.Add(child);
            added = true;
        }

        if (!added)
        {
            panel.Children.Add(CreateEmptyState(emptyText));
        }
    }

    private static UIElement WrapRow(UIElement child, Brush background)
    {
        return new Border
        {
            Background = background,
            BorderBrush = Brush(226, 230, 235),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14),
            Child = child
        };
    }

    private static UIElement CreateEmptyState(string text)
    {
        return WrapRow(
            new TextBlock
            {
                Text = text,
                FontSize = 14,
                Foreground = Brush(101, 108, 116),
                TextWrapping = TextWrapping.Wrap
            },
            Brush(248, 249, 251));
    }

    private static Border CreateTextBadge(string text, Brush background, Brush foreground)
    {
        return new Border
        {
            Background = background,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 6, 10, 6),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = text,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = foreground
            }
        };
    }



    private static string FormatTurnState(TurnState state)
    {
        return state switch
        {
            TurnState.Waiting => "Waiting",
            TurnState.Called => "Called",
            TurnState.InService => "In service",
            TurnState.Completed => "Completed",
            TurnState.Cancelled => "Cancelled",
            TurnState.NoShow => "No show",
            TurnState.Voided => "Voided",
            _ => state.ToString()
        };
    }

    private static string FormatTurnSource(TurnSource source)
    {
        return source == TurnSource.Appointment ? "Appointment" : "Walk-in";
    }

    private static string FormatTurnBarberText(Turn turn, IReadOnlyList<Barber> barbers)
    {
        if (turn.AssignedBarberId is Guid assignedBarberId)
        {
            return barbers.FirstOrDefault(barber => barber.Id == assignedBarberId)?.DisplayNameWithStation ?? "Local barber";
        }

        if (turn.State == TurnState.Waiting
            && turn.RequestedBarberIds?.Count == 1
            && turn.RequestedBarberIds.FirstOrDefault() is Guid reservedBarberId
            && reservedBarberId != Guid.Empty)
        {
            var barberName = barbers.FirstOrDefault(barber => barber.Id == reservedBarberId)?.DisplayNameWithStation ?? "local barber";
            return $"Reserved for {barberName}";
        }

        return "Unassigned";
    }

    private static string FormatReassignTicketOption(Turn turn, IReadOnlyList<Barber> barbers)
    {
        var customerName = string.IsNullOrWhiteSpace(turn.CustomerName) ? "Walk-in customer" : turn.CustomerName;
        return $"#{turn.DisplayTicketNumber} - {customerName} - {FormatTurnState(turn.State)} - {FormatTurnBarberText(turn, barbers)}";
    }

    private static string FormatReassignBarberOption(Barber barber)
    {
        return $"{barber.DisplayNameWithStation} - {FormatBarberState(barber.State)}";
    }

    private static string FormatBarberState(BarberState state)
    {
        return state switch
        {
            BarberState.NotCheckedIn => "Not checked in",
            BarberState.Available => "Available",
            BarberState.Called => "Called",
            BarberState.InService => "In service",
            BarberState.Offline => "Offline",
            _ => state.ToString()
        };
    }



    private static Brush GetTurnBackground(TurnState state)
    {
        return state switch
        {
            TurnState.Waiting => Brush(255, 247, 232),
            TurnState.Called => Brush(235, 248, 244),
            TurnState.InService => Brush(240, 244, 250),
            _ => Brush(248, 249, 251)
        };
    }

    private static Brush GetTurnForeground(TurnState state)
    {
        return state switch
        {
            TurnState.Waiting => Brush(122, 82, 21),
            TurnState.Called => Brush(17, 105, 88),
            TurnState.InService => Brush(63, 78, 97),
            _ => Brush(101, 108, 116)
        };
    }

    private static string FormatMoney(long cents, string currency)
    {
        return $"{currency} {Money.FromCents(cents):0.00}";
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        var kilobytes = bytes / 1024m;
        if (kilobytes < 1024)
        {
            return $"{kilobytes:0.0} KB";
        }

        return $"{kilobytes / 1024m:0.0} MB";
    }

    private static SolidColorBrush Brush(byte red, byte green, byte blue)
    {
        return new SolidColorBrush(ColorHelper.FromArgb(255, red, green, blue));
    }

    private sealed record ReassignTicketOption(Guid TurnId, string DisplayText);

    private sealed record ReassignBarberOption(Guid BarberId, string DisplayText);
}
