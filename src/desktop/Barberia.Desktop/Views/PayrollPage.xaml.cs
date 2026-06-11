using Barberia.Data.Models;
using Barberia.Desktop.Services;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml;
using Windows.UI.Text;

namespace Barberia.Desktop.Views;

public sealed partial class PayrollPage : Page
{
    private readonly PayrollService _payrollService;
    private PayrollWeekRange _currentRange = new(DateTimeOffset.MinValue, DateTimeOffset.MinValue);
    private PayrollSnapshot? _snapshot;
    private bool _isInitializing;

    public event EventHandler? ShellMenuRequested;

    public PayrollPage()
    {
        _payrollService = new PayrollService(LocalDesktopDatabase.CreateConnectionFactory());
        InitializeComponent();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isInitializing = true;
        _weekDatePicker.Date = DateTimeOffset.Now;
        _isInitializing = false;
        LoadWeek(DateTimeOffset.Now);
    }

    private void OnMenuButtonClick(object sender, RoutedEventArgs e)
    {
        ShellMenuRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnRecalculateClick(object sender, RoutedEventArgs e)
    {
        TryRun(() =>
        {
            _snapshot = _payrollService.GenerateOrRecalculate(_currentRange, DateTimeOffset.Now);
            RenderSnapshot("Semana recalculada.");
        });
    }

    private async void OnAddAdjustmentClick(object sender, RoutedEventArgs e)
    {
        if (_snapshot?.Period.State == PayrollPeriodState.Paid)
        {
            ShowMessage("No se pueden agregar ajustes a una semana pagada.", isError: true);
            return;
        }

        var barbers = _payrollService.ListBarbers();
        if (barbers.Count == 0)
        {
            ShowMessage("No hay barberos registrados para asignar el ajuste.", isError: true);
            return;
        }

        var barberComboBox = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            PlaceholderText = "Selecciona un barbero"
        };
        foreach (var barber in barbers)
        {
            barberComboBox.Items.Add(new ComboBoxItem { Content = barber.DisplayName, Tag = barber.Id });
        }
        barberComboBox.SelectedIndex = 0;

        var amountBox = new TextBox
        {
            Header = "Monto (+/-)",
            PlaceholderText = "Ej. 10.00 o -5.00",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var reasonBox = new TextBox
        {
            Header = "Motivo",
            PlaceholderText = "Motivo obligatorio",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        var content = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                barberComboBox,
                amountBox,
                reasonBox
            }
        };

        var dialog = new ContentDialog
        {
            Title = "Agregar ajuste manual",
            Content = content,
            PrimaryButtonText = "Agregar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        TryRun(() =>
        {
            if (barberComboBox.SelectedItem is not ComboBoxItem item || item.Tag is not Guid barberId)
            {
                throw new InvalidOperationException("Selecciona un barbero.");
            }

            if (!decimal.TryParse(amountBox.Text, out var amount))
            {
                throw new InvalidOperationException("El monto del ajuste no es valido.");
            }

            _snapshot = _payrollService.AddAdjustment(
                _currentRange,
                barberId,
                Money.ToCents(amount),
                reasonBox.Text,
                DateTimeOffset.Now);
            RenderSnapshot("Ajuste agregado.");
        });
    }

    private async void OnPayClick(object sender, RoutedEventArgs e)
    {
        if (_snapshot is null)
        {
            ShowMessage("Genera la semana antes de pagar.", isError: true);
            return;
        }

        if (_snapshot.Period.State == PayrollPeriodState.Paid)
        {
            ShowMessage("Esta semana ya fue pagada.", isError: true);
            return;
        }

        var confirmation = new ContentDialog
        {
            Title = "Registrar pago de nomina",
            Content = $"Se marcara como pagada la semana por {FormatMoney(_snapshot.Period.TotalToPayCents)}.",
            PrimaryButtonText = "Registrar pago",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        if (await confirmation.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        TryRun(() =>
        {
            _snapshot = _payrollService.PayPeriod(
                _currentRange,
                GetSelectedPaymentMethod(),
                _txtReference.Text,
                _txtNotes.Text,
                DateTimeOffset.Now);
            RenderSnapshot("Semana marcada como pagada.");
        });
    }

    private void OnWeekDateChanged(object sender, CalendarDatePickerDateChangedEventArgs args)
    {
        if (_isInitializing)
        {
            return;
        }

        if (args.NewDate.HasValue)
        {
            LoadWeek(args.NewDate.Value);
        }
    }

    private void LoadWeek(DateTimeOffset reference)
    {
        TryRun(() =>
        {
            _currentRange = _payrollService.GetWeekRange(reference);
            _snapshot = _payrollService.LoadOrGenerate(reference);
            RenderSnapshot("Semana cargada.");
        });
    }

    private void RenderSnapshot(string message)
    {
        if (_snapshot is null)
        {
            return;
        }

        var period = _snapshot.Period;
        var startFormatted = period.StartDate.ToString("dd/MM/yyyy");
        var endFormatted = period.EndDate.AddDays(-1).ToString("dd/MM/yyyy");

        _periodRangeText.Text = $"Del {startFormatted} al {endFormatted}";
        _lblReferenceWeek.Text = $"Semana de referencia (del {startFormatted} al {endFormatted}):";
        
        _statusText.Text = period.State == PayrollPeriodState.Paid ? "PAID" : "DRAFT";
        _statusBadge.Background = period.State == PayrollPeriodState.Paid
            ? Brush(230, 244, 234)
            : Brush(255, 248, 225);
        _statusText.Foreground = period.State == PayrollPeriodState.Paid
            ? Brush(19, 115, 51)
            : Brush(138, 90, 0);

        _totalServicesText.Text = period.TotalServices.ToString();
        _totalCommissionText.Text = FormatMoney(period.TotalCommissionCents);
        _totalAdjustmentsText.Text = FormatMoney(period.TotalAdjustmentsCents);
        _totalToPayText.Text = FormatMoney(period.TotalToPayCents);

        _linesPanel.Children.Clear();
        if (_snapshot.Lines.Count == 0)
        {
            _linesPanel.Children.Add(new TextBlock
            {
                Text = "No hay comisiones para esta semana.",
                FontSize = 14,
                Foreground = Brush(68, 70, 85),
                Margin = new Thickness(12)
            });
        }
        else
        {
            foreach (var line in _snapshot.Lines)
            {
                _linesPanel.Children.Add(CreateLineRow(line));
            }
        }

        var isPaid = period.State == PayrollPeriodState.Paid;
        _btnRecalculate.IsEnabled = !isPaid;
        _btnAddAdjustment.IsEnabled = !isPaid;
        _btnPay.IsEnabled = !isPaid;
        _comboPaymentMethod.IsEnabled = !isPaid;
        
        if (isPaid)
        {
            _txtReference.Text = period.PaymentReference ?? string.Empty;
        }
        else
        {
            _txtReference.Text = $"NOM-{period.StartDate:yyMMdd}-{period.Id.ToString().Substring(0, 4).ToUpper()}";
        }

        _txtNotes.IsEnabled = !isPaid;

        ShowMessage(message, isError: false);
    }

    private static UIElement CreateLineRow(PayrollLine line)
    {
        var grid = new Grid
        {
            Padding = new Thickness(12, 10, 12, 10),
            Background = Brush(255, 255, 255),
            BorderBrush = Brush(238, 238, 240),
            BorderThickness = new Thickness(0, 0, 0, 1),
            ColumnSpacing = 10
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());

        AddCell(grid, $"{line.BarberName}{(line.StationNumber is null ? string.Empty : $" (B-{line.StationNumber})")}", 0, TextAlignment.Left, FontWeights.SemiBold);
        AddCell(grid, line.ClosedServicesCount.ToString(), 1, TextAlignment.Center, FontWeights.Normal);
        AddCell(grid, FormatMoney(line.CommissionCents), 2, TextAlignment.Right, FontWeights.Normal);
        AddCell(grid, FormatMoney(line.AdjustmentsCents), 3, TextAlignment.Right, FontWeights.Normal);
        AddCell(grid, FormatMoney(line.TotalCents), 4, TextAlignment.Right, FontWeights.SemiBold);

        return grid;
    }

    private static void AddCell(Grid grid, string text, int column, TextAlignment alignment, FontWeight fontWeight)
    {
        var cell = new TextBlock
        {
            Text = text,
            FontSize = 14,
            Foreground = Brush(26, 28, 30),
            TextAlignment = alignment,
            FontWeight = fontWeight,
            TextWrapping = TextWrapping.Wrap
        };
        Grid.SetColumn(cell, column);
        grid.Children.Add(cell);
    }

    private PayrollPaymentMethod GetSelectedPaymentMethod()
    {
        if (_comboPaymentMethod.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            return tag switch
            {
                "Transfer" => PayrollPaymentMethod.Transfer,
                "Other" => PayrollPaymentMethod.Other,
                _ => PayrollPaymentMethod.Cash
            };
        }

        return PayrollPaymentMethod.Cash;
    }

    private void TryRun(Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            ShowMessage(exception.Message, isError: true);
        }
    }

    private void ShowMessage(string message, bool isError)
    {
        _messageText.Text = message;
        _messageText.Foreground = isError ? Brush(186, 26, 26) : Brush(68, 70, 85);
    }

    private static string FormatMoney(long cents)
    {
        return $"${cents / 100m:0.00}";
    }

    private static SolidColorBrush Brush(byte red, byte green, byte blue)
    {
        return new SolidColorBrush(ColorHelper.FromArgb(255, red, green, blue));
    }
}
