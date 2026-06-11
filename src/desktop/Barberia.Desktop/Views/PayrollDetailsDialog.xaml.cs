using Barberia.Data.Models;
using Barberia.Desktop.Services;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;
using Microsoft.UI.Xaml;

namespace Barberia.Desktop.Views;

public sealed partial class PayrollDetailsDialog : ContentDialog
{
    public PayrollDetailsDialog(
        PayrollSnapshot snapshot, 
        PayrollLine barberLine, 
        IReadOnlyList<PayrollDailyBreakdown> breakdown)
    {
        InitializeComponent();
        
        // Setup header
        _titleText.Text = $"Earnings Breakdown - {barberLine.BarberName}";
        _subtitleText.Text = $"{snapshot.Period.StartDate:MMM d} - {snapshot.Period.EndDate.AddDays(-1):MMM d, yyyy}";
        
        // Setup rows
        foreach (var day in breakdown)
        {
            _linesPanel.Children.Add(CreateDayRow(day));
        }

        // Setup footer (total earnings = line total)
        _totalEarningsText.Text = $"${barberLine.TotalCents / 100m:0.00}";
    }

    private Grid CreateDayRow(PayrollDailyBreakdown day)
    {
        var grid = new Grid
        {
            Padding = new Thickness(8, 12, 8, 12),
            BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(255, 238, 238, 240)),
            BorderThickness = new Thickness(0, 0, 0, 1)
        };

        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.5, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.5, GridUnitType.Star) });

        var dateText = day.Date.ToString("ddd, MMM d, yyyy");
        var commPercentage = day.SalesCents > 0 ? $"{(decimal)day.CommissionCents / day.SalesCents:P0}" : "0%";

        AddCell(grid, dateText, 0, TextAlignment.Left, Microsoft.UI.Text.FontWeights.Normal, new SolidColorBrush(ColorHelper.FromArgb(255, 26, 28, 30)));
        AddCell(grid, day.ServicesCount.ToString(), 1, TextAlignment.Right, Microsoft.UI.Text.FontWeights.Normal, new SolidColorBrush(ColorHelper.FromArgb(255, 26, 28, 30)));
        AddCell(grid, $"${day.SalesCents / 100m:0.00}", 2, TextAlignment.Right, Microsoft.UI.Text.FontWeights.Normal, new SolidColorBrush(ColorHelper.FromArgb(255, 26, 28, 30)));
        AddCell(grid, commPercentage, 3, TextAlignment.Right, Microsoft.UI.Text.FontWeights.Normal, new SolidColorBrush(ColorHelper.FromArgb(255, 26, 28, 30)));
        AddCell(grid, $"${day.CommissionCents / 100m:0.00}", 4, TextAlignment.Right, Microsoft.UI.Text.FontWeights.SemiBold, new SolidColorBrush(ColorHelper.FromArgb(255, 26, 28, 30)));

        return grid;
    }

    private void AddCell(Grid grid, string text, int column, TextAlignment alignment, Windows.UI.Text.FontWeight weight, SolidColorBrush foreground)
    {
        var tb = new TextBlock
        {
            Text = text,
            FontSize = 16,
            TextAlignment = alignment,
            FontWeight = weight,
            Foreground = foreground,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(tb, column);
        grid.Children.Add(tb);
    }
}
