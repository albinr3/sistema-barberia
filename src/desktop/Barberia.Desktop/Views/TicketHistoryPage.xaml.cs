using Barberia.Core.Domain;
using Barberia.Data;
using Barberia.Data.Models;
using Barberia.Data.Repositories;
using Barberia.Desktop.Services;
using Barberia.Desktop.Shell;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.UI.Text;

namespace Barberia.Desktop.Views;

public sealed partial class TicketHistoryPage : Page
{
    private readonly SqliteConnectionFactory _connectionFactory;
    private bool _isLoaded;
    private int _currentPage = 1;
    private const int PageSize = 20;

    public event EventHandler? ShellMenuRequested;

    public TicketHistoryPage()
    {
        InitializeComponent();
        _connectionFactory = LocalDesktopDatabase.CreateConnectionFactory();
    }

    private void OnMenuButtonClick(object sender, RoutedEventArgs args) => ShellMenuRequested?.Invoke(this, EventArgs.Empty);

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        if (_isLoaded) return;

        _statusComboBox.SelectedIndex = 0;

        try
        {
            using var connection = _connectionFactory.OpenConnection();
            var barberRepo = new LocalBarberRepository(connection);
            var barbers = barberRepo.ListAll();
            var allBarbersList = new List<BarberFilterItem> { new(null, "All barbers") };
            allBarbersList.AddRange(barbers.Select(b => new BarberFilterItem(b.Id, b.DisplayName)));
            _barberComboBox.ItemsSource = allBarbersList;
            _barberComboBox.SelectedIndex = 0;
        }
        catch
        {
            // The history page can still load without the barber filter.
        }

        _fromDatePicker.Date = DateTimeOffset.Now.Date;
        _toDatePicker.Date = DateTimeOffset.Now.Date;

        _isLoaded = true;
        LoadHistory();
    }

    private void OnSelectionFilterChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded) return;
        _currentPage = 1;
        LoadHistory();
    }

    private void OnDateFilterChanged(object sender, CalendarDatePickerDateChangedEventArgs args)
    {
        if (!_isLoaded) return;
        _currentPage = 1;
        LoadHistory();
    }

    private void OnApplyFiltersClick(object sender, RoutedEventArgs args)
    {
        _currentPage = 1;
        LoadHistory();
    }

    private void OnClearFiltersClick(object sender, RoutedEventArgs args)
    {
        _searchTextBox.Text = string.Empty;
        _statusComboBox.SelectedIndex = 0;
        _barberComboBox.SelectedIndex = 0;
        _fromDatePicker.Date = DateTimeOffset.Now.Date;
        _toDatePicker.Date = DateTimeOffset.Now.Date;
        _currentPage = 1;
        LoadHistory();
    }

    private void LoadHistory()
    {
        try
        {
            using var connection = _connectionFactory.OpenConnection();
            var repo = new LocalTicketHistoryRepository(connection);

            var from = _fromDatePicker.Date?.Date ?? DateTimeOffset.MinValue;
            var to = _toDatePicker.Date?.Date.AddDays(1) ?? DateTimeOffset.MaxValue;

            TurnState? statusFilter = null;
            if (_statusComboBox.SelectedItem is ComboBoxItem item && item.Tag is string tag && tag != "All" &&
                Enum.TryParse<TurnState>(tag, out var parsedState))
            {
                statusFilter = parsedState;
            }

            Guid? barberIdFilter = null;
            if (_barberComboBox.SelectedItem is BarberFilterItem selectedBarber && selectedBarber.Id.HasValue)
            {
                barberIdFilter = selectedBarber.Id;
            }

            int totalItems = repo.CountHistory(from, to, statusFilter, barberIdFilter, _searchTextBox.Text);
            int totalPages = (int)Math.Ceiling(totalItems / (double)PageSize);
            if (totalPages == 0) totalPages = 1;
            
            if (_currentPage > totalPages) _currentPage = totalPages;

            int offset = (_currentPage - 1) * PageSize;
            var results = repo.ListHistory(from, to, statusFilter, barberIdFilter, _searchTextBox.Text, PageSize, offset);

            _resultsCountText.Text = results.Count == 1
                ? "Showing 1 ticket"
                : $"Showing {results.Count} tickets";

            _historyRows.Children.Clear();
            if (results.Count == 0)
            {
                _historyRows.Children.Add(CreateEmptyState("No tickets found for the selected date range."));
                UpdatePagination(totalPages);
                return;
            }

            foreach (var row in results)
            {
                _historyRows.Children.Add(CreateHistoryRow(row));
            }

            UpdatePagination(totalPages);
        }
        catch (Exception ex)
        {
            _historyRows.Children.Clear();
            _resultsCountText.Text = "Showing 0 tickets";
            _historyRows.Children.Add(CreateEmptyState($"Error loading history: {ex.Message}"));
        }
    }

    private FrameworkElement CreateHistoryRow(TicketHistoryRow historyRow)
    {
        var barberName = historyRow.AssignedBarberName ?? "Unassigned";
        var customerName = string.IsNullOrWhiteSpace(historyRow.CustomerName) ? "Walk-in customer" : historyRow.CustomerName;
        var serviceName = string.IsNullOrWhiteSpace(historyRow.ServiceName) ? FormatTurnSource(historyRow.Source) : historyRow.ServiceName;

        var grid = new Grid
        {
            Padding = new Thickness(16, 14, 16, 14),
            ColumnSpacing = 12,
            Background = Brush(255, 255, 255)
        };

        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(155) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(135) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(76) });

        AddCell(grid, CreateText(historyRow.DisplayTicketNumber.ToString(), 14, FontWeights.SemiBold, Brush(0, 19, 135)), 0);
        AddCell(grid, CreateDateCell(historyRow.CheckedInAt), 1);
        AddCell(grid, CreateText(customerName, 14, FontWeights.Normal, Brush(26, 28, 30)), 2);
        AddCell(grid, CreateBarberCell(barberName, historyRow.AssignedBarberImagePath), 3);
        AddCell(grid, CreateText(serviceName, 14, FontWeights.Normal, Brush(68, 70, 85)), 4);
        AddCell(grid, CreateText(FormatAmount(historyRow.Amount), 15, FontWeights.SemiBold, Brush(26, 28, 30), HorizontalAlignment.Right), 5);
        AddCell(grid, CreateText(historyRow.PaymentMethod?.ToString() ?? "-", 14, FontWeights.Normal, Brush(68, 70, 85), HorizontalAlignment.Right), 6);
        AddCell(grid, CreateTextBadge(FormatTurnState(historyRow.FinalState), GetTurnBackground(historyRow.FinalState), GetTurnForeground(historyRow.FinalState)), 7);
        AddCell(grid, CreateActionIcon(), 8);

        var row = new Border
        {
            BorderBrush = Brush(226, 226, 229),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = grid,
            Tag = historyRow
        };

        row.Tapped += OnHistoryRowTapped;
        row.PointerEntered += (_, _) => grid.Background = Brush(243, 243, 246);
        row.PointerExited += (_, _) => grid.Background = Brush(255, 255, 255);
        ToolTipService.SetToolTip(row, "Open ticket details");
        return row;
    }

    private async void OnHistoryRowTapped(object sender, TappedRoutedEventArgs args)
    {
        if (sender is not FrameworkElement { Tag: TicketHistoryRow historyRow })
        {
            return;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = $"Ticket Details - {historyRow.DisplayTicketNumber}",
            PrimaryButtonText = "Close",
            DefaultButton = ContentDialogButton.Primary,
            Content = CreateTicketDetailsContent(historyRow)
        };

        await dialog.ShowAsync();
    }

    private static FrameworkElement CreateTicketDetailsContent(TicketHistoryRow historyRow)
    {
        var barberName = historyRow.AssignedBarberName ?? "Unassigned";
        var customerName = string.IsNullOrWhiteSpace(historyRow.CustomerName) ? "Walk-in customer" : historyRow.CustomerName;
        var serviceName = string.IsNullOrWhiteSpace(historyRow.ServiceName) ? FormatTurnSource(historyRow.Source) : historyRow.ServiceName;

        var layout = new Grid
        {
            MinWidth = 560,
            ColumnSpacing = 32
        };
        layout.ColumnDefinitions.Add(new ColumnDefinition());
        layout.ColumnDefinitions.Add(new ColumnDefinition());

        var summary = new StackPanel { Spacing = 14 };
        summary.Children.Add(CreateSectionLabel("Ticket Summary"));
        summary.Children.Add(CreateDetailRow("Customer", customerName));
        summary.Children.Add(CreateDetailRow("Barber", barberName));
        summary.Children.Add(CreateDetailRow("Service", serviceName));
        summary.Children.Add(CreateDetailRow("Source", FormatTurnSource(historyRow.Source)));
        summary.Children.Add(CreateDetailRow("Receipt", string.IsNullOrWhiteSpace(historyRow.ReceiptNumber) ? "No receipt" : historyRow.ReceiptNumber));
        summary.Children.Add(CreateDivider());
        summary.Children.Add(CreateDetailRow("Total", FormatAmount(historyRow.Amount), true));
        summary.Children.Add(CreateDetailBadgeRow("Status", FormatTurnState(historyRow.FinalState), GetTurnBackground(historyRow.FinalState), GetTurnForeground(historyRow.FinalState)));
        if (!string.IsNullOrWhiteSpace(historyRow.PaymentResultText))
        {
            summary.Children.Add(CreateDetailRow("Payment", historyRow.PaymentResultText));
        }

        var timeline = new StackPanel { Spacing = 14 };
        timeline.Children.Add(CreateSectionLabel("Service Timeline"));
        timeline.Children.Add(CreateTimelineItem("Created", historyRow.CheckedInAt));
        timeline.Children.Add(CreateTimelineItem("Service Started", historyRow.StartedAt));
        timeline.Children.Add(CreateTimelineItem("Payment Completed", historyRow.ChargedAt));
        timeline.Children.Add(CreateTimelineItem("Completed", historyRow.CompletedAt));
        timeline.Children.Add(CreateTimelineItem("Cancelled", historyRow.CancelledAt));

        Grid.SetColumn(timeline, 1);
        layout.Children.Add(summary);
        layout.Children.Add(timeline);

        return new ScrollViewer
        {
            MaxHeight = 520,
            Content = layout
        };
    }

    private static FrameworkElement CreateDateCell(DateTimeOffset date)
    {
        return new StackPanel
        {
            Spacing = 2,
            Children =
            {
                CreateText(date.ToString("MMM d, yyyy", CultureInfo.CurrentCulture), 14, FontWeights.Normal, Brush(26, 28, 30)),
                CreateText(date.ToString("h:mm tt", CultureInfo.CurrentCulture), 12, FontWeights.Normal, Brush(68, 70, 85))
            }
        };
    }

    private static FrameworkElement CreateBarberCell(string barberName, string? imagePath)
    {
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                CreateProfileAvatar(barberName, imagePath, 24),
                CreateText(barberName, 14, FontWeights.Normal, Brush(26, 28, 30))
            }
        };
    }

    private static FrameworkElement CreateActionIcon()
    {
        return new FontIcon
        {
            Glyph = "\uE712",
            FontSize = 18,
            Foreground = Brush(68, 70, 85),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    private static TextBlock CreateText(string text, double size, FontWeight weight, Brush foreground, HorizontalAlignment alignment = HorizontalAlignment.Left)
    {
        return new TextBlock
        {
            Text = text,
            FontFamily = new FontFamily("Inter"),
            FontSize = size,
            FontWeight = weight,
            Foreground = foreground,
            HorizontalAlignment = alignment,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap,
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    private static TextBlock CreateSectionLabel(string text)
    {
        return CreateText(text.ToUpperInvariant(), 12, FontWeights.SemiBold, Brush(68, 70, 85));
    }

    private static FrameworkElement CreateDetailRow(string label, string value, bool emphasize = false)
    {
        var grid = new Grid { ColumnSpacing = 16 };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());

        AddCell(grid, CreateText(label, emphasize ? 20 : 14, emphasize ? FontWeights.SemiBold : FontWeights.Normal, emphasize ? Brush(26, 28, 30) : Brush(68, 70, 85)), 0);
        AddCell(grid, CreateText(value, emphasize ? 20 : 14, emphasize ? FontWeights.SemiBold : FontWeights.Medium, emphasize ? Brush(0, 19, 135) : Brush(26, 28, 30), HorizontalAlignment.Right), 1);
        return grid;
    }

    private static FrameworkElement CreateDetailBadgeRow(string label, string text, Brush background, Brush foreground)
    {
        var grid = new Grid { ColumnSpacing = 16 };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());

        AddCell(grid, CreateText(label, 14, FontWeights.Normal, Brush(68, 70, 85)), 0);
        var badge = CreateTextBadge(text, background, foreground);
        badge.HorizontalAlignment = HorizontalAlignment.Right;
        AddCell(grid, badge, 1);
        return grid;
    }

    private static FrameworkElement CreateTimelineItem(string label, DateTimeOffset? date)
    {
        if (!date.HasValue)
        {
            return new StackPanel();
        }

        var grid = new Grid { ColumnSpacing = 10 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());

        var dot = new Border
        {
            Width = 22,
            Height = 22,
            CornerRadius = new CornerRadius(11),
            Background = Brush(223, 224, 255),
            Child = new Border
            {
                Width = 8,
                Height = 8,
                CornerRadius = new CornerRadius(4),
                Background = Brush(0, 19, 135),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };

        var text = new StackPanel
        {
            Spacing = 2,
            Children =
            {
                CreateText(label, 14, FontWeights.SemiBold, Brush(26, 28, 30)),
                CreateText(date.Value.ToString("g", CultureInfo.CurrentCulture), 12, FontWeights.Normal, Brush(68, 70, 85))
            }
        };

        AddCell(grid, dot, 0);
        AddCell(grid, text, 1);
        return grid;
    }

    private static FrameworkElement CreateDivider()
    {
        return new Border
        {
            Height = 1,
            Background = Brush(197, 197, 216),
            Margin = new Thickness(0, 4, 0, 2)
        };
    }

    private static Border CreateTextBadge(string text, Brush background, Brush foreground)
    {
        return new Border
        {
            Background = background,
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(10, 4, 10, 4),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = text,
                FontFamily = new FontFamily("Inter"),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = foreground,
                TextWrapping = TextWrapping.NoWrap
            }
        };
    }

    private static FrameworkElement CreateEmptyState(string text)
    {
        return new Border
        {
            Padding = new Thickness(16, 20, 16, 20),
            BorderBrush = Brush(226, 226, 229),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = new TextBlock
            {
                Text = text,
                FontFamily = new FontFamily("Inter"),
                FontSize = 14,
                Foreground = Brush(68, 70, 85),
                TextWrapping = TextWrapping.Wrap
            }
        };
    }

    private static void AddCell(Grid grid, FrameworkElement child, int column)
    {
        Grid.SetColumn(child, column);
        grid.Children.Add(child);
    }

    private static string FormatAmount(decimal? amount) => amount.HasValue ? $"${amount.Value:0.00}" : "-";

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
            TurnState.Completed => Brush(223, 224, 255),
            TurnState.Cancelled => Brush(220, 38, 38), // Red
            TurnState.NoShow => Brush(226, 226, 229),
            TurnState.Voided => Brush(255, 218, 214),
            _ => Brush(243, 243, 246)
        };
    }

    private static Brush GetTurnForeground(TurnState state)
    {
        return state switch
        {
            TurnState.Completed => Brush(0, 11, 98),
            TurnState.Cancelled => Brush(255, 255, 255), // White
            TurnState.Voided => Brush(147, 0, 10),
            _ => Brush(68, 70, 85)
        };
    }

    private static SolidColorBrush Brush(byte red, byte green, byte blue)
    {
        return new SolidColorBrush(ColorHelper.FromArgb(255, red, green, blue));
    }

    private void OnSearchKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key == Windows.System.VirtualKey.Enter)
        {
            _currentPage = 1;
            LoadHistory();
        }
    }

    private void UpdatePagination(int totalPages)
    {
        _paginationContainer.Children.Clear();
        if (totalPages <= 1) return;

        var prevBtn = CreatePaginationButton("\uE76B", _currentPage > 1, () => { _currentPage--; LoadHistory(); }, true);
        _paginationContainer.Children.Add(prevBtn);

        int startPage = Math.Max(1, _currentPage - 2);
        int endPage = Math.Min(totalPages, startPage + 4);
        if (endPage - startPage < 4)
        {
            startPage = Math.Max(1, endPage - 4);
        }

        if (startPage > 1)
        {
            _paginationContainer.Children.Add(CreatePaginationButton("1", true, () => { _currentPage = 1; LoadHistory(); }));
            if (startPage > 2)
            {
                _paginationContainer.Children.Add(new TextBlock { Text = "...", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 4, 0) });
            }
        }

        for (int i = startPage; i <= endPage; i++)
        {
            int pageNum = i;
            var btn = CreatePaginationButton(pageNum.ToString(), true, () => { _currentPage = pageNum; LoadHistory(); });
            if (pageNum == _currentPage)
            {
                btn.Background = Brush(0, 32, 194);
                btn.Foreground = Brush(255, 255, 255);
            }
            _paginationContainer.Children.Add(btn);
        }

        if (endPage < totalPages)
        {
            if (endPage < totalPages - 1)
            {
                _paginationContainer.Children.Add(new TextBlock { Text = "...", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 4, 0) });
            }
            _paginationContainer.Children.Add(CreatePaginationButton(totalPages.ToString(), true, () => { _currentPage = totalPages; LoadHistory(); }));
        }

        var nextBtn = CreatePaginationButton("\uE76C", _currentPage < totalPages, () => { _currentPage++; LoadHistory(); }, true);
        _paginationContainer.Children.Add(nextBtn);
    }

    private Button CreatePaginationButton(string content, bool isEnabled, Action onClick, bool isIcon = false)
    {
        var btn = new Button
        {
            IsEnabled = isEnabled,
            Background = Brush(255, 255, 255),
            BorderBrush = Brush(197, 197, 216),
            Padding = new Thickness(isIcon ? 8 : 12, 6, isIcon ? 8 : 12, 6),
            MinWidth = isIcon ? 32 : 36,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };

        if (isIcon)
        {
            btn.Content = new FontIcon { Glyph = content, FontSize = 12 };
        }
        else
        {
            btn.Content = new TextBlock { Text = content, FontFamily = new FontFamily("Inter"), FontSize = 14, FontWeight = FontWeights.Medium };
        }

        btn.Click += (s, e) => onClick();
        return btn;
    }

    private static Grid CreateProfileAvatar(string displayName, string? relativeImagePath, double size)
    {
        var avatar = new Grid
        {
            Width = size,
            Height = size,
            VerticalAlignment = VerticalAlignment.Center
        };

        avatar.Children.Add(new Ellipse
        {
            Fill = Brush(243, 243, 246),
            Stroke = Brush(226, 230, 235),
            StrokeThickness = 1
        });

        avatar.Children.Add(new TextBlock
        {
            Text = GetInitials(displayName),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = size >= 50 ? 18 : (size >= 32 ? 14 : 10),
            FontWeight = FontWeights.Bold,
            Foreground = Brush(0, 19, 135)
        });

        var imagePath = ProfileImageCatalog.ResolveImagePath(relativeImagePath);
        if (imagePath is not null)
        {
            var imageBrush = new ImageBrush
            {
                Stretch = Stretch.UniformToFill
            };
            var imageCircle = new Ellipse
            {
                Fill = imageBrush,
                Stroke = Brush(255, 255, 255),
                StrokeThickness = 1,
                Visibility = Visibility.Collapsed
            };
            avatar.Children.Add(imageCircle);
            _ = LoadProfileImageAsync(imageBrush, imageCircle, imagePath);
        }

        return avatar;
    }

    private static string GetInitials(string displayName)
    {
        var parts = displayName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(2)
            .Select(part => char.ToUpperInvariant(part[0]));

        var initials = string.Concat(parts);
        return string.IsNullOrWhiteSpace(initials) ? "?" : initials;
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

    private record BarberFilterItem(Guid? Id, string DisplayName);
}
