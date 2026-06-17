using Barberia.Data.Models;
using Barberia.Desktop.Services;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Barberia.Desktop.Views;

public sealed partial class PayrollHistoryDetailsDialog : ContentDialog
{
    public PayrollHistoryDetailsDialog(PayrollSnapshot snapshot)
    {
        InitializeComponent();

        var period = snapshot.Period;
        var refText = period.PaymentReference ?? $"NOM-{period.StartDate:yyMMdd}-{period.Id.ToString().Substring(0, 4).ToUpper()}";
        
        _titleText.Text = $"Payroll Details - {refText}";
        _subtitleText.Text = $"{period.StartDate:MMM d} - {period.EndDate.AddDays(-1):MMM d, yyyy}";

        _kpiServices.Text = period.TotalServices.ToString();
        _kpiCommission.Text = $"${period.TotalCommissionCents / 100m:0.00}";
        _kpiTotal.Text = $"${period.TotalToPayCents / 100m:0.00}";

        foreach (var line in snapshot.Lines)
        {
            _linesPanel.Children.Add(CreateLineRow(line));
        }
    }

    private Grid CreateLineRow(PayrollLine line)
    {
        var grid = new Grid
        {
            Padding = new Thickness(16, 12, 16, 12),
            BorderBrush = Brush(238, 238, 240),
            BorderThickness = new Thickness(0, 0, 0, 1)
        };

        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.5, GridUnitType.Star) });

        AddCell(grid, line.BarberName, 0, TextAlignment.Left, Microsoft.UI.Text.FontWeights.SemiBold, Brush(26, 28, 30));
        AddCell(grid, $"{line.ClosedServicesCount} svcs", 1, TextAlignment.Right, Microsoft.UI.Text.FontWeights.Normal, Brush(68, 70, 85));
        AddCell(grid, $"Comm: ${line.CommissionCents / 100m:0.00}", 2, TextAlignment.Right, Microsoft.UI.Text.FontWeights.Normal, Brush(68, 70, 85));
        AddCell(grid, $"${line.TotalCents / 100m:0.00}", 3, TextAlignment.Right, Microsoft.UI.Text.FontWeights.Bold, Brush(26, 28, 30));

        return grid;
    }

    private void AddCell(Grid grid, string text, int column, TextAlignment alignment, Windows.UI.Text.FontWeight weight, SolidColorBrush foreground)
    {
        var tb = new TextBlock
        {
            Text = text,
            FontSize = 14,
            TextAlignment = alignment,
            FontWeight = weight,
            Foreground = foreground,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.WrapWholeWords
        };
        Grid.SetColumn(tb, column);
        grid.Children.Add(tb);
    }

    private static SolidColorBrush Brush(byte r, byte g, byte b)
    {
        return new SolidColorBrush(ColorHelper.FromArgb(255, r, g, b));
    }
}
