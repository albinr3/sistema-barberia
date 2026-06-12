using Barberia.Core.Domain;
using Barberia.Data.Models;
using Barberia.Desktop.Services;
using Barberia.Desktop.Shell;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using Windows.Graphics.Imaging;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Barberia.Desktop.Views;

public sealed partial class LocalAdminPage : Page
{
    private readonly LocalAdminService _service = new();

    public event EventHandler? ShellMenuRequested;

    public LocalAdminPage()
    {
        InitializeComponent();
    }

    private void OnMenuButtonClick(object sender, RoutedEventArgs args)
    {
        ShellMenuRequested?.Invoke(this, EventArgs.Empty);
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
        _salesTotalText.Text = FormatMoney(snapshot.Cash.TotalSalesCents, snapshot.Cash.Currency);
        _salesBreakdownText.Text = $"Cash: {FormatMoney(snapshot.Cash.CashSalesCents, snapshot.Cash.Currency)} | Zelle: {FormatMoney(snapshot.Cash.ZelleSalesCents, snapshot.Cash.Currency)}";
        _lastRefreshText.Text = $"Updated: {snapshot.GeneratedAt:hh:mm tt}";
        _waitingCountText.Text = $"{snapshot.ActiveTurns.Count(turn => turn.State == TurnState.Waiting)} Waiting";
        UpdateReassignmentControls(snapshot);
        ReplaceChildren(
            _alertRows,
            snapshot.Alerts.Select(CreateAlertRow),
            "No current alerts.");
        _alertsCard.Visibility = snapshot.Alerts.Any() ? Visibility.Visible : Visibility.Collapsed;

        ReplaceChildren(
            _turnRows,
            snapshot.ActiveTurns.Select(turn => CreateTurnRow(turn, snapshot.Barbers)),
            "No active turns right now.");
        ReplaceChildren(
            _historyRows,
            snapshot.RecentTicketHistoryToday.Select(CreateHistoryRow),
            "No ticket history recorded today.");
        ReplaceChildren(
            _auditRows,
            snapshot.RecentAuditEvents.Select(CreateAuditRow),
            "No audit events recorded yet.");

        var dailyRotationEntries = snapshot.DailyRotationEntries.ToDictionary(entry => entry.BarberId);
        var staffElements = snapshot.Barbers
            .Where(b => b.IsActive)
            .Take(10)
            .Select(barber => CreateStaffRow(barber, dailyRotationEntries))
            .ToList();
        var staffChildren = new List<UIElement>();
        
        if (staffElements.Count > 0)
        {
            var grid = new Grid { ColumnSpacing = 16, RowSpacing = 16 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            for (int i = 0; i < 5; i++)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }

            for (int i = 0; i < staffElements.Count; i++)
            {
                var child = (FrameworkElement)staffElements[i];
                Grid.SetRow(child, i % 5);
                Grid.SetColumn(child, i / 5);
                grid.Children.Add(child);
            }
            staffChildren.Add(grid);
        }

        ReplaceChildren(
            _staffRows,
            staffChildren,
            "No barbers registered in the local database.");
    }


    private void UpdateReassignmentControls(LocalAdminSnapshot snapshot)
    {
        var ticketOptions = new[] { new ReassignTicketOption(Guid.Empty, "Select Ticket") }
            .Concat(snapshot.ActiveTurns
                .Where(turn => turn.State is TurnState.Waiting or TurnState.Called)
                .Select(turn => new ReassignTicketOption(turn.Id, FormatReassignTicketOption(turn, snapshot.Barbers))))
            .ToArray();
        var barberOptions = snapshot.Barbers
            .Where(barber => barber.IsActive && barber.State != BarberState.Offline)
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
            Padding = new Thickness(20, 12, 20, 12),
            Children =
            {
                new TextBlock
                {
                    Text = turn.DisplayTicketNumber.ToString(),
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brush(0, 19, 135),
                    VerticalAlignment = VerticalAlignment.Center,
                    TextWrapping = TextWrapping.WrapWholeWords
                }
            }
        };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.9, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var clientText = new TextBlock
        {
            Text = customerName,
            FontSize = 14,
            Foreground = Brush(26, 28, 30),
            TextWrapping = TextWrapping.WrapWholeWords,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(clientText, 1);
        row.Children.Add(clientText);

        var statusBadge = CreateTextBadge(
            FormatTurnState(turn.State),
            GetTurnBackground(turn.State),
            GetTurnForeground(turn.State));
        Grid.SetColumn(statusBadge, 2);
        row.Children.Add(statusBadge);

        var cancelButton = CreateSmallActionButton("Cancel");
        cancelButton.Background = Brush(255, 218, 214);
        cancelButton.BorderBrush = Brush(186, 26, 26);
        cancelButton.BorderThickness = new Thickness(1);
        cancelButton.Foreground = Brush(147, 0, 10);
        cancelButton.IsEnabled = turn.State is TurnState.Waiting or TurnState.Called or TurnState.InService;
        cancelButton.Click += (_, _) => ExecuteAdminAction(() => _service.CancelTurn(turn.Id), "Cancelled");

        var assignedText = new TextBlock
        {
            Text = barberName,
            FontSize = 14,
            Foreground = Brush(68, 70, 85),
            TextWrapping = TextWrapping.WrapWholeWords,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(assignedText, 3);
        row.Children.Add(assignedText);

        Grid.SetColumn(cancelButton, 4);
        row.Children.Add(cancelButton);

        return new Border
        {
            Background = Brush(255, 255, 255),
            BorderBrush = Brush(238, 238, 240),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Child = row
        };
    }

    private void OnReassignmentSelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        UpdateReassignmentButtonState();
    }

    private void OnReassignTicketClick(object sender, RoutedEventArgs args)
    {
        if (_reassignTicketComboBox.SelectedItem is not ReassignTicketOption ticketOption || ticketOption.TurnId == Guid.Empty)
        {
            return;
        }

        if (_reassignBarberComboBox.SelectedItem is not ReassignBarberOption barberOption)
        {
            return;
        }

        ExecuteAdminAction(
            () => _service.ReassignTurn(ticketOption.TurnId, barberOption.BarberId),
            "Reassigned");
    }

    private void UpdateReassignmentButtonState()
    {
        _reassignButton.IsEnabled =
            _reassignTicketComboBox.SelectedItem is ReassignTicketOption ticketOption && ticketOption.TurnId != Guid.Empty
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

    private void OnManageBarbersClick(object sender, RoutedEventArgs args)
    {
        if (App.MainWindowInstance is MainWindow mainWindow)
        {
            mainWindow.NavigateTo(ShellModuleKey.Barbers);
        }
    }

    private void OnManageServicesClick(object sender, RoutedEventArgs args)
    {
        if (App.MainWindowInstance is MainWindow mainWindow)
        {
            mainWindow.NavigateTo(ShellModuleKey.Services);
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

    private UIElement CreateStaffRow(Barber barber, IReadOnlyDictionary<Guid, DailyRotationEntry> dailyRotationEntries)
    {
        var isOnline = barber.IsActive && barber.State != BarberState.Offline;
        var initial = string.IsNullOrWhiteSpace(barber.DisplayName)
            ? "?"
            : barber.DisplayName.Trim()[0].ToString().ToUpperInvariant();

        var row = new Grid
        {
            ColumnSpacing = 12,
            Opacity = isOnline ? 1 : 0.62
        };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var avatar = new Grid
        {
            Width = 48,
            Height = 48
        };
        avatar.Children.Add(new Border
        {
            Width = 48,
            Height = 48,
            Background = isOnline ? Brush(223, 224, 255) : Brush(232, 232, 234),
            BorderBrush = isOnline ? Brush(0, 19, 135) : Brush(197, 197, 216),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(999),
            Child = new TextBlock
            {
                Text = initial,
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                Foreground = isOnline ? Brush(0, 19, 135) : Brush(68, 70, 85),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        });

        var imagePath = ProfileImageCatalog.ResolveImagePath(barber.ProfileImagePath);
        if (imagePath is not null)
        {
            var imageBrush = new ImageBrush
            {
                Stretch = Stretch.UniformToFill
            };
            var imageCircle = new Ellipse
            {
                Width = 48,
                Height = 48,
                Fill = imageBrush,
                Stroke = Brush(255, 255, 255),
                StrokeThickness = 2,
                Visibility = Visibility.Collapsed
            };
            avatar.Children.Add(imageCircle);
            _ = LoadProfileImageAsync(imageBrush, imageCircle, imagePath);
        }

        avatar.Children.Add(new Ellipse
        {
            Width = 12,
            Height = 12,
            Fill = isOnline ? Brush(0, 19, 135) : Brush(117, 118, 135),
            Stroke = Brush(255, 255, 255),
            StrokeThickness = 2,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom
        });
        row.Children.Add(avatar);

        var details = new StackPanel
        {
            MinWidth = 96,
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock
                {
                    Text = barber.DisplayNameWithStation,
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brush(26, 28, 30),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    TextWrapping = TextWrapping.WrapWholeWords
                },
                new TextBlock
                {
                    Text = FormatBarberState(barber.State),
                    FontSize = 12,
                    Foreground = isOnline ? Brush(68, 70, 85) : Brush(117, 118, 135),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    TextWrapping = TextWrapping.WrapWholeWords
                },
                new TextBlock
                {
                    Text = FormatDailyRotationText(barber, dailyRotationEntries),
                    FontSize = 12,
                    Foreground = Brush(101, 108, 116),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    TextWrapping = TextWrapping.WrapWholeWords
                }
            }
        };
        Grid.SetColumn(details, 1);
        row.Children.Add(details);

        var toggleSwitch = new ToggleSwitch
        {
            Width = 46,
            MinWidth = 46,
            IsOn = isOnline,
            OnContent = null,
            OffContent = null,
            IsEnabled = barber.IsActive && barber.State is not BarberState.Called and not BarberState.InService,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };

        toggleSwitch.Toggled += (_, _) =>
        {
            if (toggleSwitch.IsOn)
            {
                ExecuteAdminAction(() => _service.MarkBarberAvailable(barber.Id), "Available");
            }
            else
            {
                ExecuteAdminAction(() => _service.MarkBarberOffline(barber.Id), "Offline");
            }
        };

        Grid.SetColumn(toggleSwitch, 2);
        row.Children.Add(toggleSwitch);

        return row;
    }

    private static UIElement CreateHistoryRow(TicketHistoryRow historyRow)
    {
        var barberName = historyRow.AssignedBarberName ?? "Unassigned";
        var customerName = string.IsNullOrWhiteSpace(historyRow.CustomerName) ? "Walk-in customer" : historyRow.CustomerName;
        var serviceName = string.IsNullOrWhiteSpace(historyRow.ServiceName) ? FormatTurnSource(historyRow.Source) : historyRow.ServiceName;

        var row = new Grid
        {
            ColumnSpacing = 14
        };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var iconBox = new Border
        {
            Width = 40,
            Height = 40,
            Background = GetTurnBackground(historyRow.FinalState),
            CornerRadius = new CornerRadius(999),
            Child = new FontIcon
            {
                Glyph = historyRow.FinalState switch
                {
                    TurnState.Completed => "\uE73E",
                    TurnState.Cancelled => "\uE711",
                    TurnState.NoShow => "\uE77A",
                    _ => "\uE8A5"
                },
                FontSize = 18,
                Foreground = GetTurnForeground(historyRow.FinalState),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        row.Children.Add(iconBox);

        var details = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                new TextBlock
                {
                    Text = $"Ticket {historyRow.DisplayTicketNumber} - {customerName} - {serviceName}",
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brush(26, 28, 30),
                    TextWrapping = TextWrapping.WrapWholeWords
                },
                new TextBlock
                {
                    Text = $"{barberName} | Created: {historyRow.CheckedInAt:hh:mm tt}" +
                           (historyRow.StartedAt.HasValue ? $" | Service started: {historyRow.StartedAt:hh:mm tt}" : "") +
                           (historyRow.ChargedAt.HasValue ? $" | Charged: {historyRow.ChargedAt:hh:mm tt}" : "") +
                           (historyRow.CancelledAt.HasValue ? $" | Cancelled: {historyRow.CancelledAt:hh:mm tt}" : ""),
                    FontSize = 12,
                    Foreground = Brush(117, 118, 135),
                    TextWrapping = TextWrapping.WrapWholeWords
                }
            }
        };
        Grid.SetColumn(details, 1);
        row.Children.Add(details);

        var rightStack = new StackPanel
        {
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };

        rightStack.Children.Add(CreateTextBadge(
            FormatTurnState(historyRow.FinalState),
            GetTurnBackground(historyRow.FinalState),
            GetTurnForeground(historyRow.FinalState)));

        if (historyRow.Amount.HasValue)
        {
            var amountStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right };
            amountStack.Children.Add(new TextBlock
            {
                Text = $"${historyRow.Amount.Value:0.00}",
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brush(19, 115, 51),
                HorizontalAlignment = HorizontalAlignment.Right
            });

            if (historyRow.PaymentMethod.HasValue)
            {
                amountStack.Children.Add(new TextBlock
                {
                    Text = historyRow.PaymentMethod.Value.ToString(),
                    FontSize = 12,
                    Foreground = Brush(117, 118, 135),
                    HorizontalAlignment = HorizontalAlignment.Right
                });
            }

            rightStack.Children.Add(amountStack);
        }

        Grid.SetColumn(rightStack, 2);
        row.Children.Add(rightStack);

        return WrapRow(row, Brush(255, 255, 255));
    }

    private void ExecuteAdminAction(Action action, string successStatus = "Updated")
    {
        try
        {
            action();
            LoadAdmin();
        }
        catch (Exception)
        {
            // Action blocked
        }
    }

    private void ShowError(string message)
    {
        _activeTurnsText.Text = "0";
        _checkInsText.Text = "0";
        _availableBarbersText.Text = "0/0";
        _salesTotalText.Text = "$0";
        _salesBreakdownText.Text = "Cash: $0 | Zelle: $0";
        _lastRefreshText.Text = "No local snapshot loaded";
        _waitingCountText.Text = "0 Waiting";
        _reassignTicketComboBox.ItemsSource = null;
        _reassignTicketComboBox.IsEnabled = false;
        _reassignBarberComboBox.ItemsSource = null;
        _reassignBarberComboBox.IsEnabled = false;
        _reassignButton.IsEnabled = false;
        _turnRows.Children.Clear();
        _auditRows.Children.Clear();
        _historyRows.Children.Clear();
        _alertRows.Children.Clear();
        _staffRows.Children.Clear();
        _alertsCard.Visibility = Visibility.Visible;
        _alertRows.Children.Add(CreateEmptyState("Could not read alerts."));
        _turnRows.Children.Add(CreateEmptyState("Could not read active turns."));
        _auditRows.Children.Add(CreateEmptyState(message));
        _historyRows.Children.Add(CreateEmptyState("Could not read ticket history."));
        _staffRows.Children.Add(CreateEmptyState("Could not read staff roster."));
    }

    private static Button CreateSmallActionButton(string text)
    {
        return new Button
        {
            Content = text,
            MinHeight = 36,
            MinWidth = 82,
            Padding = new Thickness(10, 6, 10, 6),
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

    private static string FormatDailyRotationText(
        Barber barber,
        IReadOnlyDictionary<Guid, DailyRotationEntry> dailyRotationEntries)
    {
        return dailyRotationEntries.TryGetValue(barber.Id, out var entry)
            ? $"Arrival #{entry.QueuePosition + 1} - {entry.ArrivedAt:hh:mm tt}"
            : "Not checked in today";
    }

    private static Brush GetTurnBackground(TurnState state)
    {
        return state switch
        {
            TurnState.Waiting => Brush(255, 247, 232),
            TurnState.Called => Brush(232, 232, 234),
            TurnState.InService => Brush(223, 224, 255),
            TurnState.Completed => Brush(230, 244, 234),
            TurnState.Cancelled => Brush(255, 218, 214),
            TurnState.NoShow => Brush(255, 248, 225),
            _ => Brush(248, 249, 251)
        };
    }

    private static Brush GetTurnForeground(TurnState state)
    {
        return state switch
        {
            TurnState.Waiting => Brush(122, 82, 21),
            TurnState.Called => Brush(26, 28, 30),
            TurnState.InService => Brush(0, 32, 194),
            TurnState.Completed => Brush(19, 115, 51),
            TurnState.Cancelled => Brush(147, 0, 10),
            TurnState.NoShow => Brush(138, 90, 0),
            _ => Brush(101, 108, 116)
        };
    }

    private static string FormatMoney(long cents, string currency)
    {
        return $"${Money.FromCents(cents):0}";
    }



    private static SolidColorBrush Brush(byte red, byte green, byte blue)
    {
        return new SolidColorBrush(ColorHelper.FromArgb(255, red, green, blue));
    }

    private static async Task LoadProfileImageAsync(ImageBrush imageBrush, UIElement imageElement, string fullPath)
    {
        try
        {
            await using var fileStream = File.OpenRead(fullPath);
            using var imageStream = fileStream.AsRandomAccessStream();
            var decoder = await BitmapDecoder.CreateAsync(imageStream);
            var bitmap = await decoder.GetSoftwareBitmapAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied);
            var source = new SoftwareBitmapSource();
            await source.SetBitmapAsync(bitmap);
            imageBrush.ImageSource = source;
            imageElement.Visibility = Visibility.Visible;
        }
        catch
        {
            imageBrush.ImageSource = null;
            imageElement.Visibility = Visibility.Collapsed;
        }
    }

    private sealed record ReassignTicketOption(Guid TurnId, string DisplayText);

    private sealed record ReassignBarberOption(Guid BarberId, string DisplayText);
}
