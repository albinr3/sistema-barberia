using Barberia.Data.Models;
using Barberia.Data.Reports;
using Barberia.Desktop.Services;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Barberia.Desktop.Views;

public sealed partial class ReportsPage : Page
{
    private readonly AdminReportsService _service = new();

    public ReportsPage()
    {
        InitializeComponent();
        _reportDatePicker.Date = DateTimeOffset.Now;
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        LoadReport();
    }

    private void OnRefreshClick(object sender, RoutedEventArgs args)
    {
        LoadReport();
    }

    private void LoadReport()
    {
        try
        {
            var snapshot = _service.LoadDailyReport(_reportDatePicker.Date);
            ShowSnapshot(snapshot);
            SetStatus("Local", success: true);
        }
        catch (Exception exception)
        {
            ShowError(exception.Message);
        }
    }

    private void ShowSnapshot(LocalAdminReportSnapshot snapshot)
    {
        _checkInsText.Text = snapshot.Operations.CheckIns.ToString();
        _completedText.Text = snapshot.Operations.CompletedServices.ToString();
        _cashText.Text = FormatMoney(snapshot.Cash.TotalAmountCents, snapshot.Cash.Currency);
        _commissionText.Text = FormatMoney(snapshot.Cash.CommissionCents, snapshot.Cash.Currency);
        _walkInsText.Text = snapshot.Operations.WalkIns.ToString();
        _appointmentsText.Text = snapshot.Operations.Appointments.ToString();
        _activeTurnsText.Text = snapshot.Operations.ActiveTurns.ToString();
        _issuesText.Text = (snapshot.Operations.NoShows + snapshot.Operations.Cancelled).ToString();
        _lastRefreshText.Text = $"Actualizado: {snapshot.GeneratedAt:hh:mm tt}";
        _messageText.Text = snapshot.Cash.PaymentsMissingCommission == 0
            ? $"Pagos registrados: {snapshot.Cash.PaymentCount}. Cash drawer: {snapshot.Cash.CashDrawerOpenCount}."
            : $"Pagos registrados: {snapshot.Cash.PaymentCount}. Comisiones pendientes: {snapshot.Cash.PaymentsMissingCommission}.";

        ReplaceChildren(
            _barberRows,
            snapshot.Barbers.Select(row => CreateBarberRow(row, snapshot.Cash.Currency)),
            "Sin barberos registrados en la base local.");
        ReplaceChildren(
            _paymentRows,
            snapshot.RecentPayments.Select(CreatePaymentRow),
            "Sin pagos en efectivo para esta fecha.");
    }

    private static UIElement CreateBarberRow(BarberReportRow row, string currency)
    {
        return new Border
        {
            Background = row.ServicesClosed > 0 ? Brush(255, 255, 255) : Brush(248, 249, 251),
            BorderBrush = Brush(226, 230, 235),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14),
            Child = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(),
                    new ColumnDefinition { Width = GridLength.Auto }
                },
                Children =
                {
                    new StackPanel
                    {
                        Spacing = 3,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = row.DisplayNameWithStation,
                                FontSize = 18,
                                FontWeight = FontWeights.SemiBold,
                                Foreground = Brush(30, 31, 34)
                            },
                            new TextBlock
                            {
                                Text = $"{row.ServicesClosed} cierres - {FormatMoney(row.CommissionCents, currency)} comision",
                                FontSize = 13,
                                Foreground = Brush(101, 108, 116)
                            }
                        }
                    },
                    CreateTextBadge(FormatMoney(row.CashCollectedCents, currency), Brush(255, 247, 232), Brush(122, 82, 21))
                }
            }
        };
    }

    private static UIElement CreatePaymentRow(CashPaymentReportRow row)
    {
        var commissionText = row.CommissionCents is null
            ? "Comision pendiente"
            : $"{FormatMoney(row.CommissionCents.Value, row.Currency)} comision";

        return new Border
        {
            Background = Brush(255, 255, 255),
            BorderBrush = row.CommissionCents is null ? Brush(242, 181, 88) : Brush(226, 230, 235),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14),
            Child = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(),
                    new ColumnDefinition { Width = GridLength.Auto }
                },
                Children =
                {
                    new StackPanel
                    {
                        Spacing = 3,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = $"{row.TicketNumber} - {row.BarberNameWithStation}",
                                FontSize = 17,
                                FontWeight = FontWeights.SemiBold,
                                Foreground = Brush(30, 31, 34)
                            },
                            new TextBlock
                            {
                                Text = $"{row.CollectedAt:hh:mm tt} - {commissionText}",
                                FontSize = 13,
                                Foreground = Brush(101, 108, 116)
                            }
                        }
                    },
                    CreateTextBadge(FormatMoney(row.AmountCents, row.Currency), Brush(235, 248, 244), Brush(17, 105, 88))
                }
            }
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

    private void ShowError(string message)
    {
        _checkInsText.Text = "0";
        _completedText.Text = "0";
        _cashText.Text = "USD 0.00";
        _commissionText.Text = "USD 0.00";
        _walkInsText.Text = "0";
        _appointmentsText.Text = "0";
        _activeTurnsText.Text = "0";
        _issuesText.Text = "0";
        _lastRefreshText.Text = "Sin datos actualizados";
        _messageText.Text = message;
        _barberRows.Children.Clear();
        _paymentRows.Children.Clear();
        _barberRows.Children.Add(CreateEmptyState("No se pudo leer la base local."));
        _paymentRows.Children.Add(CreateEmptyState(message));
        SetStatus("Error local", success: false);
    }

    private void SetStatus(string text, bool success)
    {
        _statusBadgeText.Text = text;
        _statusBadge.Background = success ? Brush(235, 248, 244) : Brush(255, 240, 238);
        _statusBadge.BorderBrush = success ? Brush(181, 224, 211) : Brush(231, 170, 162);
        _statusBadgeText.Foreground = success ? Brush(17, 105, 88) : Brush(154, 58, 47);
    }

    private static UIElement CreateEmptyState(string text)
    {
        return new Border
        {
            Background = Brush(248, 249, 251),
            BorderBrush = Brush(226, 230, 235),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16),
            Child = new TextBlock
            {
                Text = text,
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brush(101, 108, 116)
            }
        };
    }

    private static UIElement CreateTextBadge(string text, Brush background, Brush foreground)
    {
        return new Border
        {
            Background = background,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 6, 10, 6),
            VerticalAlignment = VerticalAlignment.Top,
            Child = new TextBlock
            {
                Text = text,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = foreground
            }
        };
    }

    private static string FormatMoney(long cents, string currency)
    {
        return $"{currency} {Money.FromCents(cents):0.00}";
    }

    private static SolidColorBrush Brush(byte red, byte green, byte blue)
    {
        return new SolidColorBrush(ColorHelper.FromArgb(255, red, green, blue));
    }
}
