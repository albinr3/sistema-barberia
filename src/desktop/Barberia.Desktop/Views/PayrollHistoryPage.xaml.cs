using Barberia.Data.Models;
using Barberia.Desktop.Services;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Barberia.Desktop.Views;

public sealed partial class PayrollHistoryPage : Page
{
    private const double WideContentMaxWidth = 1200;
    private const double MediumLayoutThreshold = 900;
    private const double NarrowLayoutThreshold = 720;
    private const double HistoryTableMinWidth = 860;

    private readonly PayrollService _payrollService;

    public PayrollHistoryPage()
    {
        _payrollService = new PayrollService(LocalDesktopDatabase.CreateConnectionFactory());
        InitializeComponent();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyResponsiveLayout(ActualWidth);
        LoadHistory();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs args)
    {
        ApplyResponsiveLayout(args.NewSize.Width);
    }

    private void LoadHistory()
    {
        try
        {
            var periods = _payrollService.ListHistoricalPeriods();
            _historyLinesPanel.Children.Clear();

            if (periods.Count == 0)
            {
                _historyLinesPanel.Children.Add(new TextBlock
                {
                    Text = "No hay nominas procesadas.",
                    Margin = new Thickness(24),
                    Foreground = Brush(68, 70, 85)
                });
                return;
            }

            foreach (var period in periods)
            {
                _historyLinesPanel.Children.Add(CreatePeriodRow(period));
            }
        }
        catch (Exception)
        {
            _historyLinesPanel.Children.Clear();
            _historyLinesPanel.Children.Add(new TextBlock
            {
                Text = "No se pudo cargar el historial de nomina.",
                Margin = new Thickness(24),
                Foreground = Brush(186, 26, 26)
            });
        }
    }

    private Grid CreatePeriodRow(PayrollPeriod period)
    {
        var grid = new Grid
        {
            Padding = new Thickness(24, 16, 24, 16),
            BorderBrush = Brush(238, 238, 240),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Background = new SolidColorBrush(Colors.Transparent)
        };

        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.5, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var refText = period.PaymentReference ?? $"NOM-{period.StartDate:yyMMdd}-{period.Id.ToString().Substring(0, 4).ToUpper()}";
        var dateRange = $"{period.StartDate:MMM d} - {period.EndDate.AddDays(-1):MMM d, yyyy}";
        var processedDate = period.PaidAt?.ToString("MMM d, yyyy") ?? period.GeneratedAt.ToString("MMM d, yyyy");
        var totalPayout = $"${period.TotalToPayCents / 100m:0.00}";

        AddCell(grid, refText, 0, TextAlignment.Left, Microsoft.UI.Text.FontWeights.Medium, Brush(68, 70, 85));
        AddCell(grid, dateRange, 1, TextAlignment.Left, Microsoft.UI.Text.FontWeights.Medium, Brush(26, 28, 30));
        AddCell(grid, processedDate, 2, TextAlignment.Left, Microsoft.UI.Text.FontWeights.Normal, Brush(68, 70, 85));
        AddCell(grid, period.TotalServices.ToString(), 3, TextAlignment.Right, Microsoft.UI.Text.FontWeights.Normal, Brush(26, 28, 30));
        AddCell(grid, totalPayout, 4, TextAlignment.Right, Microsoft.UI.Text.FontWeights.Bold, Brush(0, 19, 135));

        var statusPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(16, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        var statusBorder = new Border
        {
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(10, 4, 10, 4),
            BorderThickness = new Thickness(1)
        };
        var statusText = new TextBlock
        {
            FontSize = 12,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        };

        if (period.State == PayrollPeriodState.Paid)
        {
            statusBorder.Background = Brush(232, 245, 233);
            statusBorder.BorderBrush = Brush(200, 230, 201);
            statusText.Foreground = Brush(27, 94, 32);
            statusText.Text = "Paid";
        }
        else
        {
            statusBorder.Background = Brush(227, 242, 253);
            statusBorder.BorderBrush = Brush(187, 222, 251);
            statusText.Foreground = Brush(13, 71, 161);
            statusText.Text = "Processing";
        }

        statusBorder.Child = statusText;
        statusPanel.Children.Add(statusBorder);
        Grid.SetColumn(statusPanel, 5);
        grid.Children.Add(statusPanel);

        var actionBtn = new Button
        {
            Content = "Details",
            Background = new SolidColorBrush(Colors.Transparent),
            BorderThickness = new Thickness(0),
            Foreground = Brush(0, 19, 135),
            FontWeight = Microsoft.UI.Text.FontWeights.Medium,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        actionBtn.Click += async (s, e) =>
        {
            try
            {
                var snapshot = _payrollService.Load(new PayrollWeekRange(period.StartDate, period.EndDate));
                var dialog = new PayrollHistoryDetailsDialog(snapshot)
                {
                    XamlRoot = XamlRoot
                };
                await dialog.ShowAsync();
            }
            catch (Exception)
            {
                // Fallback handling if navigation is already completing
            }
        };

        Grid.SetColumn(actionBtn, 6);
        grid.Children.Add(actionBtn);

        return grid;
    }

    private void ApplyResponsiveLayout(double width)
    {
        var effectiveWidth = width > 0 ? width : WideContentMaxWidth;
        var useMediumLayout = effectiveWidth < MediumLayoutThreshold;
        var useNarrowLayout = effectiveWidth < NarrowLayoutThreshold;
        var horizontalPadding = useNarrowLayout ? 20 : useMediumLayout ? 32 : 48;
        var contentWidth = Math.Min(WideContentMaxWidth, effectiveWidth);

        _contentGrid.Width = contentWidth;
        _contentGrid.Padding = new Thickness(horizontalPadding, useNarrowLayout ? 32 : 48, horizontalPadding, 72);

        ApplyHeaderLayout(useMediumLayout);
        ApplyFiltersLayout(useNarrowLayout);

        _tableBorder.Width = Math.Max(HistoryTableMinWidth, contentWidth - (horizontalPadding * 2));
    }

    private void ApplyHeaderLayout(bool useMediumLayout)
    {
        _headerActionsColumn.Width = useMediumLayout ? new GridLength(0) : GridLength.Auto;
        _exportButton.HorizontalAlignment = useMediumLayout ? HorizontalAlignment.Stretch : HorizontalAlignment.Right;
        _exportButton.VerticalAlignment = useMediumLayout ? VerticalAlignment.Top : VerticalAlignment.Bottom;

        Grid.SetColumnSpan(_headerTextPanel, useMediumLayout ? 2 : 1);
        Grid.SetColumn(_exportButton, useMediumLayout ? 0 : 1);
        Grid.SetRow(_exportButton, useMediumLayout ? 1 : 0);
        Grid.SetColumnSpan(_exportButton, useMediumLayout ? 2 : 1);
    }

    private void ApplyFiltersLayout(bool useNarrowLayout)
    {
        _filtersGrid.ColumnSpacing = useNarrowLayout ? 0 : 16;
        _filterStatusColumn.Width = useNarrowLayout ? new GridLength(0) : GridLength.Auto;
        _statusComboBox.Width = useNarrowLayout ? double.NaN : 150;
        _statusComboBox.HorizontalAlignment = useNarrowLayout ? HorizontalAlignment.Stretch : HorizontalAlignment.Left;

        Grid.SetColumn(_statusComboBox, useNarrowLayout ? 0 : 1);
        Grid.SetRow(_statusComboBox, useNarrowLayout ? 1 : 0);
        Grid.SetColumnSpan(_searchBox, useNarrowLayout ? 2 : 1);
    }

    private static void AddCell(Grid grid, string text, int column, TextAlignment alignment, Windows.UI.Text.FontWeight weight, SolidColorBrush foreground)
    {
        var cell = new TextBlock
        {
            Text = text,
            FontSize = 14,
            TextAlignment = alignment,
            FontWeight = weight,
            Foreground = foreground,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.WrapWholeWords
        };
        Grid.SetColumn(cell, column);
        grid.Children.Add(cell);
    }

    private void OnBackClick(object sender, RoutedEventArgs e)
    {
        if (App.MainWindowInstance is MainWindow mainWindow)
        {
            mainWindow.NavigateTo(Shell.ShellModuleKey.Payroll);
        }
    }

    private static SolidColorBrush Brush(byte red, byte green, byte blue)
    {
        return new SolidColorBrush(ColorHelper.FromArgb(255, red, green, blue));
    }
}
