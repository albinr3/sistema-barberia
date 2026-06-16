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
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.UI.Text;

namespace Barberia.Desktop.Views;

public sealed partial class PayrollPage : Page
{
    private const double WideContentMaxWidth = 1200;
    private const double MediumLayoutThreshold = 1000;
    private const double NarrowLayoutThreshold = 720;
    private const double PayrollTableMinWidth = 760;

    private readonly PayrollService _payrollService;
    private PayrollWeekRange _currentRange = new(DateTimeOffset.MinValue, DateTimeOffset.MinValue);
    private PayrollSnapshot? _snapshot;
    private bool _isInitializing;
    private readonly List<PayrollAdjustment> _tempAdjustments = new();

    public event EventHandler? ShellMenuRequested;

    public PayrollPage()
    {
        _payrollService = new PayrollService(LocalDesktopDatabase.CreateConnectionFactory());
        InitializeComponent();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyResponsiveLayout(ActualWidth);
        _isInitializing = true;
        _weekDatePicker.Date = OperationalClock.Now;
        _isInitializing = false;
        LoadWeek(OperationalClock.Now);
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs args)
    {
        ApplyResponsiveLayout(args.NewSize.Width);
    }

    private void OnMenuButtonClick(object sender, RoutedEventArgs e)
    {
        ShellMenuRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnHistoryClick(object sender, RoutedEventArgs e)
    {
        if (App.MainWindowInstance is MainWindow mainWindow)
        {
            mainWindow.NavigateTo(Shell.ShellModuleKey.PayrollHistory);
        }
    }

    private void OnRecalculateClick(object sender, RoutedEventArgs e)
    {
        TryRun(() =>
        {
            LoadPendingAdjustments(_currentRange);
            _snapshot = _payrollService.GeneratePreview(_currentRange, _tempAdjustments, OperationalClock.Now);
            RenderSnapshot("Week recalculated.");
        });
    }

    private async void OnAddAdjustmentClick(object sender, RoutedEventArgs e)
    {
        if (_snapshot?.Period.State == PayrollPeriodState.Paid)
        {
            ShowMessage("Cannot add adjustments to a paid week.", isError: true);
            return;
        }

        var barbers = _payrollService.ListBarbers();
        if (barbers.Count == 0)
        {
            ShowMessage("No barbers registered to assign the adjustment.", isError: true);
            return;
        }

        var barberComboBox = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            PlaceholderText = "Select a barber"
        };
        foreach (var barber in barbers)
        {
            barberComboBox.Items.Add(new ComboBoxItem { Content = barber.DisplayName, Tag = barber.Id });
        }
        barberComboBox.SelectedIndex = 0;

        var amountBox = new TextBox
        {
            Header = "Amount (+/-)",
            PlaceholderText = "e.g. 10.00 or -5.00",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var reasonBox = new TextBox
        {
            Header = "Reason",
            PlaceholderText = "Reason required",
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
            Title = "Add manual adjustment",
            Content = content,
            PrimaryButtonText = "Add",
            CloseButtonText = "Cancel",
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
                throw new InvalidOperationException("Please select a barber.");
            }

            if (!decimal.TryParse(amountBox.Text, out var amount))
            {
                throw new InvalidOperationException("The adjustment amount is invalid.");
            }

            _payrollService.AddPendingAdjustment(
                _currentRange,
                barberId,
                Money.ToCents(amount),
                reasonBox.Text,
                OperationalClock.Now);

            LoadPendingAdjustments(_currentRange);
            _snapshot = _payrollService.GeneratePreview(_currentRange, _tempAdjustments, OperationalClock.Now);
            RenderSnapshot("Adjustment added.");
        });
    }

    private async void OnPayClick(object sender, RoutedEventArgs e)
    {
        if (_snapshot is null)
        {
            ShowMessage("Generate the week before paying.", isError: true);
            return;
        }

        if (_snapshot.Period.State == PayrollPeriodState.Paid)
        {
            ShowMessage("This week was already paid.", isError: true);
            return;
        }

        var reference = $"NOM-{_snapshot.Period.StartDate:yyMMdd}-{_snapshot.Period.Id.ToString().Substring(0, 4).ToUpper()}";

        var confirmation = new ContentDialog
        {
            Title = "Process All Payments",
            Content = $"The week will be marked as paid for {FormatMoney(_snapshot.Period.TotalToPayCents)}.\nReference: {reference}",
            PrimaryButtonText = "Register payment (Cash)",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        if (await confirmation.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        bool success = false;
        TryRun(() =>
        {
            _snapshot = _payrollService.PayPeriod(
                _currentRange,
                _tempAdjustments,
                PayrollPaymentMethod.Cash,
                reference,
                null,
                OperationalClock.Now);
            _tempAdjustments.Clear();
            RenderSnapshot("Week marked as paid.");
            success = true;
        });

        if (success)
        {
            var successDialog = new ContentDialog
            {
                Title = "Payment successful",
                Content = "The payroll has been paid and registered successfully.",
                CloseButtonText = "OK",
                XamlRoot = XamlRoot
            };
            await successDialog.ShowAsync();
        }
    }

    private async void OnViewDetailsClick(PayrollLine line)
    {
        if (_snapshot == null) return;

        try
        {
            var breakdown = _payrollService.GetBarberDailyBreakdown(_snapshot, line.BarberId);
            var dialog = new PayrollDetailsDialog(_snapshot, line, breakdown)
            {
                XamlRoot = XamlRoot
            };
            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            ShowMessage($"Error loading details: {ex.Message}", true);
        }
    }

    private void OnWeekDateChanged(object sender, CalendarDatePickerDateChangedEventArgs args)
    {
        if (_isInitializing) return;

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
            LoadPendingAdjustments(_currentRange);
            _snapshot = _payrollService.LoadOrGenerate(reference, _tempAdjustments);
            RenderSnapshot("Week loaded.");
        });
    }

    private void LoadPendingAdjustments(PayrollWeekRange range)
    {
        _tempAdjustments.Clear();
        _tempAdjustments.AddRange(_payrollService.ListPendingAdjustments(range));
    }

    private void RenderSnapshot(string message)
    {
        if (_snapshot is null)
        {
            return;
        }

        var period = _snapshot.Period;
        var startFormatted = period.StartDate.ToString("ddd, MMM d");
        var endFormatted = period.EndDate.AddDays(-1).ToString("ddd, MMM d, yyyy");

        _periodRangeText.Text = $"{startFormatted} - {endFormatted}";

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
                Text = "No commissions for this week.",
                FontSize = 14,
                Foreground = Brush(68, 70, 85),
                Margin = new Thickness(12)
            });
        }
        else
        {
            foreach (var line in _snapshot.Lines)
            {
                _linesPanel.Children.Add(CreateLineRow(line, period.State == PayrollPeriodState.Paid));
            }
        }

        var isPaid = period.State == PayrollPeriodState.Paid;
        _btnRecalculate.IsEnabled = !isPaid;
        _btnAddAdjustment.IsEnabled = !isPaid;
        _btnPay.IsEnabled = !isPaid;

        var reference = $"NOM-{period.StartDate:yyMMdd}-{period.Id.ToString().Substring(0, 4).ToUpper()}";
        if (_referenceTextBox != null)
        {
            _referenceTextBox.Text = reference;
        }

        ShowMessage(message, isError: false);
    }

    private UIElement CreateLineRow(PayrollLine line, bool isPaid)
    {
        var grid = new Grid
        {
            Padding = new Thickness(16, 12, 16, 12),
            Background = Brush(255, 255, 255),
            BorderBrush = Brush(238, 238, 240),
            BorderThickness = new Thickness(0, 0, 0, 1)
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.5, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.5, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.5, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var staffPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        var avatar = CreateProfileAvatar(line.BarberName, line.BarberImagePath, 32);
        var textPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        textPanel.Children.Add(new TextBlock { Text = line.BarberName, FontWeight = FontWeights.SemiBold, FontSize = 14, Foreground = Brush(26, 28, 30), TextWrapping = TextWrapping.WrapWholeWords });
        staffPanel.Children.Add(avatar);
        staffPanel.Children.Add(textPanel);
        Grid.SetColumn(staffPanel, 0);
        grid.Children.Add(staffPanel);

        var stationBorder = new Border { Background = Brush(238, 238, 240), CornerRadius = new CornerRadius(4), Padding = new Thickness(8, 2, 8, 2), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
        stationBorder.Child = new TextBlock { Text = line.StationNumber is null ? "N/A" : $"B-{line.StationNumber}", FontSize = 12, FontWeight = FontWeights.Medium, Foreground = Brush(26, 28, 30) };
        Grid.SetColumn(stationBorder, 1);
        grid.Children.Add(stationBorder);

        AddCell(grid, line.ClosedServicesCount.ToString(), 2, TextAlignment.Right, FontWeights.Normal, Brush(26, 28, 30));
        AddCell(grid, FormatMoney(line.SalesGeneratedCents), 3, TextAlignment.Right, FontWeights.Normal, Brush(26, 28, 30));
        AddCell(grid, FormatMoney(line.CommissionCents), 4, TextAlignment.Right, FontWeights.Medium, Brush(26, 28, 30));
        AddCell(grid, FormatMoney(line.TotalCents), 5, TextAlignment.Right, FontWeights.Bold, Brush(26, 28, 30));

        var actionsPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var btnView = new Button { Content = new FontIcon { Glyph = "\uE890", FontSize = 16 }, Background = Brush(243, 243, 246), BorderThickness = new Thickness(0) };
        ToolTipService.SetToolTip(btnView, "View Details");
        btnView.Click += (s, e) => OnViewDetailsClick(line);
        actionsPanel.Children.Add(btnView);

        Grid.SetColumn(actionsPanel, 6);
        grid.Children.Add(actionsPanel);

        return grid;
    }

    private void ApplyResponsiveLayout(double width)
    {
        var effectiveWidth = width > 0 ? width : WideContentMaxWidth;
        var useMediumLayout = effectiveWidth < MediumLayoutThreshold;
        var useNarrowLayout = effectiveWidth < NarrowLayoutThreshold;
        var horizontalPadding = useNarrowLayout ? 20 : useMediumLayout ? 32 : 48;
        var topPadding = useNarrowLayout ? 72 : 48;
        var contentWidth = Math.Min(WideContentMaxWidth, effectiveWidth);

        _contentGrid.Width = contentWidth;
        _contentGrid.Padding = new Thickness(horizontalPadding, topPadding, horizontalPadding, 72);

        ApplyHeaderLayout(useMediumLayout, useNarrowLayout);
        ApplyKpiLayout(useMediumLayout, useNarrowLayout);
        ApplyFiltersLayout(useNarrowLayout);

        var tableWidth = Math.Max(PayrollTableMinWidth, contentWidth - (horizontalPadding * 2));
        _tableBorder.Width = tableWidth;
    }

    private void ApplyHeaderLayout(bool useMediumLayout, bool useNarrowLayout)
    {
        _headerActionsColumn.Width = useMediumLayout ? new GridLength(0) : GridLength.Auto;
        _headerActionsPanel.Orientation = useNarrowLayout ? Orientation.Vertical : Orientation.Horizontal;
        _headerActionsPanel.HorizontalAlignment = useMediumLayout ? HorizontalAlignment.Stretch : HorizontalAlignment.Right;
        _headerActionsPanel.VerticalAlignment = useMediumLayout ? VerticalAlignment.Top : VerticalAlignment.Bottom;

        Grid.SetColumnSpan(_headerTextPanel, useMediumLayout ? 2 : 1);
        Grid.SetColumn(_headerActionsPanel, useMediumLayout ? 0 : 1);
        Grid.SetRow(_headerActionsPanel, useMediumLayout ? 1 : 0);
        Grid.SetColumnSpan(_headerActionsPanel, useMediumLayout ? 2 : 1);
    }

    private void ApplyKpiLayout(bool useMediumLayout, bool useNarrowLayout)
    {
        _kpiGrid.ColumnSpacing = useNarrowLayout ? 0 : useMediumLayout ? 16 : 24;
        _kpiGrid.RowSpacing = useMediumLayout ? 16 : 24;

        if (useNarrowLayout)
        {
            SetKpiColumns(1);
            SetKpiPosition(_servicesCard, 0, 0);
            SetKpiPosition(_commissionCard, 1, 0);
            SetKpiPosition(_adjustmentsCard, 2, 0);
            SetKpiPosition(_netPayCard, 3, 0);
            return;
        }

        if (useMediumLayout)
        {
            SetKpiColumns(2);
            SetKpiPosition(_servicesCard, 0, 0);
            SetKpiPosition(_commissionCard, 0, 1);
            SetKpiPosition(_adjustmentsCard, 1, 0);
            SetKpiPosition(_netPayCard, 1, 1);
            return;
        }

        SetKpiColumns(4);
        SetKpiPosition(_servicesCard, 0, 0);
        SetKpiPosition(_commissionCard, 0, 1);
        SetKpiPosition(_adjustmentsCard, 0, 2);
        SetKpiPosition(_netPayCard, 0, 3);
    }

    private void ApplyFiltersLayout(bool useNarrowLayout)
    {
        _filtersGrid.ColumnSpacing = useNarrowLayout ? 0 : 12;
        _weekDatePicker.Width = useNarrowLayout ? double.NaN : 200;
        _weekDatePicker.HorizontalAlignment = useNarrowLayout ? HorizontalAlignment.Stretch : HorizontalAlignment.Left;
        _messageText.HorizontalAlignment = useNarrowLayout ? HorizontalAlignment.Left : HorizontalAlignment.Right;
        _messageText.Margin = useNarrowLayout ? new Thickness(0) : new Thickness(0, 0, 16, 0);

        Grid.SetColumn(_messageText, useNarrowLayout ? 0 : 1);
        Grid.SetRow(_messageText, useNarrowLayout ? 1 : 0);
    }

    private void SetKpiColumns(int visibleColumns)
    {
        _kpiColumn0.Width = new GridLength(1, GridUnitType.Star);
        _kpiColumn1.Width = visibleColumns >= 2 ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        _kpiColumn2.Width = visibleColumns >= 4 ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        _kpiColumn3.Width = visibleColumns >= 4 ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
    }

    private static void SetKpiPosition(FrameworkElement element, int row, int column)
    {
        Grid.SetRow(element, row);
        Grid.SetColumn(element, column);
    }

    private static void AddCell(Grid grid, string text, int column, TextAlignment alignment, FontWeight fontWeight, SolidColorBrush foreground)
    {
        var cell = new TextBlock
        {
            Text = text,
            FontSize = 14,
            Foreground = foreground,
            TextAlignment = alignment,
            FontWeight = fontWeight,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        };
        Grid.SetColumn(cell, column);
        grid.Children.Add(cell);
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
}
