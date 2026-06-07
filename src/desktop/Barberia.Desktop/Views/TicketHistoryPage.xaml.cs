using Barberia.Core.Domain;
using Barberia.Data;
using Barberia.Data.Models;
using Barberia.Data.Repositories;
using Barberia.Desktop.Services;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Barberia.Desktop.Views;

public sealed partial class TicketHistoryPage : Page
{
    private readonly SqliteConnectionFactory _connectionFactory;
    private bool _isLoaded;

    public TicketHistoryPage()
    {
        InitializeComponent();
        _connectionFactory = LocalDesktopDatabase.CreateConnectionFactory();
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        if (_isLoaded) return;
        
        _statusComboBox.SelectedIndex = 0;
        
        try
        {
            using var connection = _connectionFactory.OpenConnection();
            var barberRepo = new LocalBarberRepository(connection);
            var barbers = barberRepo.ListAll();
            var allBarbersList = new List<Barber> { new Barber(Guid.Empty, "All barbers", BarberState.Offline, 0, 0, null, null, null, false) };
            allBarbersList.AddRange(barbers);
            _barberComboBox.ItemsSource = allBarbersList;
            _barberComboBox.SelectedIndex = 0;
        }
        catch
        {
            // Ignore for now
        }

        _fromDatePicker.Date = DateTimeOffset.Now.Date;
        _toDatePicker.Date = DateTimeOffset.Now.Date;
        
        _isLoaded = true;
        LoadHistory();
    }

    private void OnSelectionFilterChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded) return;
        LoadHistory();
    }

    private void OnDateFilterChanged(object sender, DatePickerValueChangedEventArgs args)
    {
        if (!_isLoaded) return;
        LoadHistory();
    }

    private void LoadHistory()
    {
        try
        {
            using var connection = _connectionFactory.OpenConnection();
            var repo = new LocalTicketHistoryRepository(connection);

            var from = _fromDatePicker.Date.Date;
            var to = _toDatePicker.Date.Date.AddDays(1); // Include the whole "to" day

            TurnState? statusFilter = null;
            if (_statusComboBox.SelectedItem is ComboBoxItem item && item.Tag is string tag && tag != "All")
            {
                if (Enum.TryParse<TurnState>(tag, out var parsedState))
                {
                    statusFilter = parsedState;
                }
            }

            Guid? barberIdFilter = null;
            if (_barberComboBox.SelectedItem is Barber selectedBarber && selectedBarber.Id != Guid.Empty)
            {
                barberIdFilter = selectedBarber.Id;
            }

            var results = repo.ListHistory(from, to, statusFilter, barberIdFilter);

            _resultsCountText.Text = $"Showing {results.Count} tickets";

            _historyRows.Children.Clear();
            if (results.Count == 0)
            {
                _historyRows.Children.Add(CreateEmptyState("No tickets found for the selected date range."));
                return;
            }

            foreach (var row in results)
            {
                _historyRows.Children.Add(CreateHistoryRow(row));
            }
        }
        catch (Exception ex)
        {
            _historyRows.Children.Clear();
            _historyRows.Children.Add(CreateEmptyState($"Error loading history: {ex.Message}"));
        }
    }

    private static UIElement CreateHistoryRow(TicketHistoryRow historyRow)
    {
        var barberName = historyRow.AssignedBarberName ?? "Unassigned";
        var customerName = string.IsNullOrWhiteSpace(historyRow.CustomerName) ? "Walk-in customer" : historyRow.CustomerName;

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
                            Text = $"{historyRow.DisplayTicketNumber} - {customerName}",
                            FontSize = 17,
                            FontWeight = FontWeights.SemiBold,
                            Foreground = Brush(30, 31, 34),
                            TextWrapping = TextWrapping.WrapWholeWords
                        },
                        new TextBlock
                        {
                            Text = $"{FormatTurnSource(historyRow.Source)} - {barberName}",
                            FontSize = 13,
                            Foreground = Brush(101, 108, 116),
                            TextWrapping = TextWrapping.WrapWholeWords
                        },
                        new TextBlock
                        {
                            Text = $"Created: {historyRow.CheckedInAt:g}" +
                                   (historyRow.StartedAt.HasValue ? $" | Service started: {historyRow.StartedAt:g}" : "") +
                                   (historyRow.ChargedAt.HasValue ? $" | Charged: {historyRow.ChargedAt:g}" : "") +
                                   (historyRow.CancelledAt.HasValue ? $" | Cancelled: {historyRow.CancelledAt:g}" : ""),
                            FontSize = 12,
                            Foreground = Brush(151, 158, 166),
                            TextWrapping = TextWrapping.WrapWholeWords
                        }
                    }
                }
            }
        };
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var rightStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        if (historyRow.Amount.HasValue)
        {
            rightStack.Children.Add(new TextBlock
            {
                Text = $"${historyRow.Amount.Value:0.00}",
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brush(17, 105, 88),
                VerticalAlignment = VerticalAlignment.Center
            });
        }

        rightStack.Children.Add(CreateTextBadge(
            FormatTurnState(historyRow.FinalState),
            GetTurnBackground(historyRow.FinalState),
            GetTurnForeground(historyRow.FinalState)));

        Grid.SetColumn(rightStack, 1);
        row.Children.Add(rightStack);

        return WrapRow(row, Brush(255, 255, 255));
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

    private static SolidColorBrush Brush(byte red, byte green, byte blue)
    {
        return new SolidColorBrush(ColorHelper.FromArgb(255, red, green, blue));
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
}
