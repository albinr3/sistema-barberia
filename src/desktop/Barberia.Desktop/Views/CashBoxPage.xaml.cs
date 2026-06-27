using Barberia.Data.Models;
using Barberia.Desktop.Services;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Windowing;
using System;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using Windows.Graphics;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace Barberia.Desktop.Views;

public sealed partial class CashBoxPage : Page
{
    private const double NarrowLayoutThreshold = 900;
    private const int ServiceOptionColumnCount = 3;
    private const uint ErrorBeepType = 0xFFFFFFFF;

    private static readonly SolidColorBrush SuccessTextBrush = Brush(17, 105, 88);
    private static readonly SolidColorBrush ErrorTextBrush = Brush(154, 58, 47);
    private static readonly SolidColorBrush NeutralTextBrush = Brush(26, 28, 30);

    private readonly CashBoxCloseService _service = new();
    private readonly MediaPlayer _successPlayer;
    private IReadOnlyList<Service> _services = [];
    private Service? _selectedService;
    private ToggleButton? _selectedServiceButton;
    private decimal _additionalAmount;
    private bool _hasLoadedTicket;
    private string? _loadedTicketInput;
    private CashBoxSnapshot? _cashBoxSnapshot;
    private bool _hasPromptedOpeningCash;
    private static CustomerCashBoxWindow? _customerWindow;

    public event EventHandler? ShellMenuRequested;

    public CashBoxPage()
    {
        InitializeComponent();
        _successPlayer = new MediaPlayer();
        _successPlayer.Source = MediaSource.CreateFromUri(new Uri(System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "cashbox.mp3")));
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        ApplyResponsiveLayout(ActualWidth);
        LoadCashBox();
        ShowReadyState();
        DispatcherQueue.TryEnqueue(() => _ticketInput.Focus(FocusState.Programmatic));
        OpenCustomerDisplay();
    }

    private void OpenCustomerDisplay()
    {
        if (_customerWindow != null) return;

        _customerWindow = new CustomerCashBoxWindow();
        _customerWindow.Closed += (s, e) => _customerWindow = null;
        
        var appWindow = _customerWindow.AppWindow;
        if (appWindow != null)
        {
            var displayAreas = DisplayArea.FindAll();
            var mainMonitor = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary);
            
            DisplayArea secondMonitor = null;
            if (mainMonitor != null)
            {
                for (int i = 0; i < displayAreas.Count; i++)
                {
                    var d = displayAreas[i];
                    if (d.DisplayId.Value != mainMonitor.DisplayId.Value)
                    {
                        secondMonitor = d;
                        break;
                    }
                }
            }
            if (secondMonitor != null)
            {
                appWindow.MoveAndResize(new RectInt32(
                    secondMonitor.WorkArea.X, 
                    secondMonitor.WorkArea.Y,
                    secondMonitor.WorkArea.Width,
                    secondMonitor.WorkArea.Height
                ));
                
                if (appWindow.Presenter is OverlappedPresenter presenter)
                {
                    presenter.Maximize();
                }
            }
        }
        
        _customerWindow.Activate();
    }

    private async void OnCloseClick(object sender, RoutedEventArgs args)
    {
        await CloseService();
    }

    private void OnPayLaterClick(object sender, RoutedEventArgs args)
    {
        MarkPendingPayment();
    }

    private async void OnPendingPaymentsClick(object sender, RoutedEventArgs args)
    {
        await ShowPendingPaymentsDialog();
    }

    private void OnPrintDayClick(object sender, RoutedEventArgs args)
    {
        try
        {
            _service.PrintDayReport();
            SetMessage("Day report printed successfully.", SuccessTextBrush);
        }
        catch (Exception exception)
        {
            ShowError(exception.Message);
        }
    }

    private void OnMenuButtonClick(object sender, RoutedEventArgs args)
    {
        ShellMenuRequested?.Invoke(this, EventArgs.Empty);
    }

    private async void OnReprintReceiptsClick(object sender, RoutedEventArgs args)
    {
        var passwordBox = new PasswordBox { PlaceholderText = "Admin password" };
        var dialog = new ContentDialog
        {
            Title = "Reprint Receipts",
            Content = passwordBox,
            PrimaryButtonText = "Submit",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            if (passwordBox.Password == "G1234")
            {
                var window = new ReceiptReprintWindow();
                window.Activate();
            }
            else
            {
                ShowError("Invalid password.");
            }
        }
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs args)
    {
        ApplyResponsiveLayout(args.NewSize.Width);
    }

    private void OnAdditionalClick(object sender, RoutedEventArgs args)
    {
        if (sender is not ToggleButton button
            || button.Tag is not string tag
            || !decimal.TryParse(tag, out var selectedAmount))
        {
            return;
        }

        _additionalAmount = button.IsChecked == true ? selectedAmount : 0;
        SyncAdditionalButtons(button);
        UpdateServiceTotal();
    }

    private void OnPaymentMethodSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var method = CustomerPaymentMethod.Cash;
        if (_paymentMethodComboBox?.SelectedItem is ComboBoxItem item && item.Tag?.ToString() == "1")
        {
            if (_paymentReferenceInput != null) _paymentReferenceInput.Visibility = Visibility.Visible;
            method = CustomerPaymentMethod.Zelle;
        }
        else
        {
            if (_paymentReferenceInput != null)
            {
                _paymentReferenceInput.Visibility = Visibility.Collapsed;
                _paymentReferenceInput.Text = string.Empty;
            }
        }
        CashBoxCustomerDisplayState.Current.UpdatePaymentMethod(method);
    }

    private void OnInputKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs args)
    {
        if (args.Key == Windows.System.VirtualKey.Enter)
        {
            LookupTicket();
            args.Handled = true;
        }
    }

    private void OnTicketInputLostFocus(object sender, RoutedEventArgs args)
    {
        if (!string.IsNullOrWhiteSpace(_ticketInput.Text))
        {
            LookupTicket();
        }
    }

    private void OnTicketInputTextChanged(object sender, TextChangedEventArgs args)
    {
        if (!_hasLoadedTicket)
        {
            return;
        }

        if (!string.Equals(NormalizeTicketInput(_ticketInput.Text), _loadedTicketInput, StringComparison.OrdinalIgnoreCase))
        {
            ClearTicketDetails();
        }
    }

    private void LoadCashBox()
    {
        try
        {
            var snapshot = _service.Load();
            _cashBoxSnapshot = snapshot;
            _services = snapshot.Services;
            SyncServiceOptions();
            _lastRefreshText.Text = $"Updated: {snapshot.LoadedAt:hh:mm tt}";
            SetPendingPaymentsButtonText(snapshot.PendingPaymentCount);
            UpdateCashBoxOpeningSummary(snapshot);
            SetMessage(
                snapshot.IsCashBoxOpened
                    ? "Waiting for ticket and service."
                    : "Open the cash box with today's opening cash before collecting payments.",
                snapshot.IsCashBoxOpened ? SuccessTextBrush : ErrorTextBrush);
            UpdateServiceTotal();

            if (!snapshot.IsCashBoxOpened && !_hasPromptedOpeningCash)
            {
                _hasPromptedOpeningCash = true;
                DispatcherQueue.TryEnqueue(() => OnOpenCashBoxClick(this, new RoutedEventArgs()));
            }
        }
        catch (Exception exception)
        {
            ShowError(exception.Message);
        }
    }

    private void MarkPendingPayment()
    {
        if (_selectedService is not Service selectedService)
        {
            ShowError("Select a service.");
            DispatcherQueue.TryEnqueue(() => _ticketInput.Focus(FocusState.Programmatic));
            return;
        }

        try
        {
            var result = _service.MarkServicePendingPayment(_ticketInput.Text, selectedService.Id, _additionalAmount);

            CashBoxCustomerDisplayState.Current.SetPendingPayment();

            _serviceReceiptText.Text = result.AdditionalAmount > 0
                ? $"{result.ServiceName} {result.ServicePrice:0.00} + addition {result.AdditionalAmount:0.00}"
                : $"{result.ServiceName} {result.ServicePrice:0.00}";
            SetMessage($"{result.DisplayTicketNumber} - {result.BarberStationCode} - {result.Message}", SuccessTextBrush);

            _ticketInput.Text = string.Empty;
            ClearTicketDetails(preserveCustomerDisplay: true);
            _additionalAmount = 0;
            SyncAdditionalButtons(null);
            if (_paymentMethodComboBox != null) _paymentMethodComboBox.SelectedIndex = 0;
            UpdateServiceTotal();
            LoadCashBox();
        }
        catch (Exception exception)
        {
            ShowError(exception.Message);
        }
        finally
        {
            DispatcherQueue.TryEnqueue(() => _ticketInput.Focus(FocusState.Programmatic));
        }
    }

    private async Task ShowPendingPaymentsDialog()
    {
        IReadOnlyList<PendingServicePaymentRow> pendingRows;
        try
        {
            pendingRows = _service.ListPendingPayments();
        }
        catch (Exception exception)
        {
            ShowError(exception.Message);
            return;
        }

        if (pendingRows.Count == 0)
        {
            SetMessage("No pending payments for the current business day.", NeutralTextBrush);
            LoadCashBox();
            return;
        }

        var selectedIds = new HashSet<Guid>();
        var selectedTotalText = new TextBlock
        {
            FontSize = 28,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = Brush(0, 32, 194),
            Text = "Selected total: $0.00"
        };
        var selectedCountText = new TextBlock
        {
            FontSize = 14,
            Foreground = Brush(68, 70, 85),
            Text = "0 tickets selected"
        };

        var collectorInput = new TextBox
        {
            Header = "Collected by station",
            PlaceholderText = "Station number, e.g. 1",
            MinHeight = 56,
            FontSize = 20,
            Padding = new Thickness(14, 8, 14, 8)
        };
        var collectorStatusText = new TextBlock
        {
            Text = "Enter the station number before collecting.",
            FontSize = 14,
            Foreground = Brush(154, 58, 47),
            TextWrapping = TextWrapping.WrapWholeWords
        };

        PendingPaymentCollectorLookupResult? collector = null;
        var paymentMethodComboBox = new ComboBox
        {
            Header = "Payment method",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            SelectedIndex = 0,
            MinHeight = 56,
            FontSize = 18,
            Padding = new Thickness(12, 6, 12, 6)
        };
        paymentMethodComboBox.Items.Add(new ComboBoxItem { Content = "Cash", Tag = "0" });
        paymentMethodComboBox.Items.Add(new ComboBoxItem { Content = "Zelle", Tag = "1" });

        var paymentReferenceInput = new TextBox
        {
            Header = "Zelle Reference",
            PlaceholderText = "Optional",
            Visibility = Visibility.Collapsed,
            MinHeight = 56,
            FontSize = 18,
            Padding = new Thickness(14, 8, 14, 8)
        };

        var collectButton = new Button
        {
            Content = "Collect Selected",
            IsEnabled = false,
            MinHeight = 64,
            FontSize = 18,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = Brush(0, 96, 223),
            Foreground = Brush(255, 255, 255)
        };
        var cancelButton = new Button
        {
            Content = "Cancel",
            MinHeight = 64,
            FontSize = 18,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        var dialog = new ContentDialog
        {
            Title = $"Pending Payments ({pendingRows.Count})",
            XamlRoot = XamlRoot
        };
        dialog.Resources["ContentDialogMaxWidth"] = 860.0;
        var shouldCollect = false;

        void UpdateCollectState()
        {
            var total = pendingRows
                .Where(row => selectedIds.Contains(row.Id))
                .Sum(row => row.Amount);
            selectedTotalText.Text = $"Selected total: ${total:0.00}";
            selectedCountText.Text = selectedIds.Count == 1
                ? "1 ticket selected"
                : $"{selectedIds.Count} tickets selected";
            collectButton.IsEnabled = selectedIds.Count > 0 && collector is not null;
        }

        void UpdateCollector()
        {
            collector = null;
            var stationInput = collectorInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(stationInput))
            {
                collectorStatusText.Text = "Enter the station number before collecting.";
                collectorStatusText.Foreground = ErrorTextBrush;
                UpdateCollectState();
                return;
            }

            try
            {
                collector = _service.LookupPendingPaymentCollector(stationInput);
                collectorStatusText.Text = $"Collected by {collector.BarberStationCode} - {collector.BarberName}";
                collectorStatusText.Foreground = SuccessTextBrush;
            }
            catch (Exception exception)
            {
                collectorStatusText.Text = exception.Message;
                collectorStatusText.Foreground = ErrorTextBrush;
            }

            UpdateCollectState();
        }

        paymentMethodComboBox.SelectionChanged += (_, _) =>
        {
            paymentReferenceInput.Visibility =
                paymentMethodComboBox.SelectedItem is ComboBoxItem item && item.Tag?.ToString() == "1"
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            if (paymentReferenceInput.Visibility == Visibility.Collapsed)
            {
                paymentReferenceInput.Text = string.Empty;
            }
        };
        collectorInput.TextChanged += (_, _) => UpdateCollector();

        var listPanel = new StackPanel { Spacing = 10 };
        foreach (var row in pendingRows)
        {
            var checkbox = new CheckBox
            {
                Tag = row.Id,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                MinHeight = 92,
                Padding = new Thickness(10, 12, 10, 12),
                Content = CreatePendingPaymentRowContent(row)
            };
            checkbox.Checked += (_, _) =>
            {
                selectedIds.Add(row.Id);
                UpdateCollectState();
            };
            checkbox.Unchecked += (_, _) =>
            {
                selectedIds.Remove(row.Id);
                UpdateCollectState();
            };
            listPanel.Children.Add(checkbox);
        }

        var summaryPanel = new Grid
        {
            ColumnSpacing = 16,
            Padding = new Thickness(16),
            Background = Brush(245, 247, 255)
        };
        summaryPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        summaryPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var summaryTextPanel = new StackPanel { Spacing = 2 };
        summaryTextPanel.Children.Add(selectedTotalText);
        summaryTextPanel.Children.Add(selectedCountText);
        summaryPanel.Children.Add(summaryTextPanel);

        var inputPanel = new Grid
        {
            ColumnSpacing = 14
        };
        inputPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        inputPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(collectorInput, 0);
        Grid.SetColumn(paymentMethodComboBox, 1);
        inputPanel.Children.Add(collectorInput);
        inputPanel.Children.Add(paymentMethodComboBox);

        var buttonPanel = new Grid
        {
            ColumnSpacing = 14
        };
        buttonPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        buttonPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(collectButton, 0);
        Grid.SetColumn(cancelButton, 1);
        buttonPanel.Children.Add(collectButton);
        buttonPanel.Children.Add(cancelButton);

        var content = new StackPanel
        {
            Spacing = 18,
            MinWidth = 500
        };
        content.Children.Add(new ScrollViewer
        {
            MaxHeight = 430,
            Content = listPanel
        });
        content.Children.Add(summaryPanel);
        content.Children.Add(inputPanel);
        content.Children.Add(collectorStatusText);
        content.Children.Add(paymentReferenceInput);
        content.Children.Add(buttonPanel);
        dialog.Content = content;

        collectButton.Click += (_, _) =>
        {
            shouldCollect = true;
            dialog.Hide();
        };
        cancelButton.Click += (_, _) => dialog.Hide();

        UpdateCollectState();
        var dialogResult = await dialog.ShowAsync();
        if (!shouldCollect || dialogResult == ContentDialogResult.None && !shouldCollect)
        {
            return;
        }

        if (collector is null || !int.TryParse(collectorInput.Text.Trim(), out var collectorStationNumber))
        {
            ShowError("Enter a valid station number for the barber collecting these pending payments.");
            return;
        }

        var paymentMethod = CustomerPaymentMethod.Cash;
        if (paymentMethodComboBox.SelectedItem is ComboBoxItem selectedMethod && selectedMethod.Tag?.ToString() == "1")
        {
            paymentMethod = CustomerPaymentMethod.Zelle;
        }

        var reference = string.IsNullOrWhiteSpace(paymentReferenceInput.Text) ? null : paymentReferenceInput.Text.Trim();
        
        decimal tenderedAmount = 0;
        decimal changeAmount = 0;
        if (paymentMethod == CustomerPaymentMethod.Cash)
        {
            var totalAmount = pendingRows.Where(row => selectedIds.Contains(row.Id)).Sum(row => row.Amount);
            var changeDialog = new ChangeCalculationDialog(totalAmount)
            {
                XamlRoot = this.XamlRoot
            };
            var changeDialogResult = await changeDialog.ShowAsync();
            if (changeDialogResult != ContentDialogResult.Primary)
            {
                return;
            }
            tenderedAmount = changeDialog.TenderedAmount;
            changeAmount = changeDialog.ChangeAmount;
        }

        try
        {
            var result = _service.CollectPendingPayments(selectedIds.ToArray(), paymentMethod, reference, collectorStationNumber, tenderedAmount, changeAmount);
            _successPlayer.Play();
            if (result.HardwareFailureMessage != null)
            {
                SetMessage($"Pending payments were collected by {result.CollectorBarberStationCode} - {result.CollectorBarberName}, but printer/drawer failed. Register the incident.\n{result.HardwareFailureMessage}", Brush(255, 140, 0));
            }
            else
            {
                SetMessage($"{result.PaymentCount} pending payment(s) collected by {result.CollectorBarberStationCode} - {result.CollectorBarberName}. Receipt {result.ReceiptNumber}.", SuccessTextBrush);
            }

            LoadCashBox();
        }
        catch (Exception exception)
        {
            ShowError(exception.Message);
        }
    }

    private async Task CloseService()
    {
        if (_selectedService is not Service selectedService)
        {
            ShowError("Select a service.");
            DispatcherQueue.TryEnqueue(() => _ticketInput.Focus(FocusState.Programmatic));
            return;
        }

        var paymentMethod = CustomerPaymentMethod.Cash;
        if (_paymentMethodComboBox?.SelectedItem is ComboBoxItem item && item.Tag?.ToString() == "1")
        {
            paymentMethod = CustomerPaymentMethod.Zelle;
        }
        var reference = _paymentReferenceInput?.Text;
        if (string.IsNullOrWhiteSpace(reference)) reference = null;

        decimal tenderedAmount = 0;
        decimal changeAmount = 0;
        if (paymentMethod == CustomerPaymentMethod.Cash)
        {
            var totalAmount = selectedService.DesktopPrice + _additionalAmount;
            var dialog = new ChangeCalculationDialog(totalAmount)
            {
                XamlRoot = this.XamlRoot
            };
            var dialogResult = await dialog.ShowAsync();
            if (dialogResult != ContentDialogResult.Primary)
            {
                return;
            }
            tenderedAmount = dialog.TenderedAmount;
            changeAmount = dialog.ChangeAmount;
        }

        try
        {
            var result = _service.CloseService(_ticketInput.Text, selectedService.Id, _additionalAmount, paymentMethod, reference, tenderedAmount, changeAmount);

            CashBoxCustomerDisplayState.Current.SetCompleted();

            _serviceReceiptText.Text = result.AdditionalAmount > 0
                ? $"{result.ServiceName} {result.ServicePrice:0.00} + addition {result.AdditionalAmount:0.00}"
                : $"{result.ServiceName} {result.ServicePrice:0.00}";
            _successPlayer.Play();

            if (result.HardwareFailureMessage != null)
            {
                SetMessage($"La venta fue registrada, pero falló la impresora/gaveta. Registre el incidente.\n{result.HardwareFailureMessage}", Brush(255, 140, 0)); // Orange for warning
            }
            else
            {
                SetMessage($"{result.DisplayTicketNumber} - {result.BarberStationCode} - {result.Message}", SuccessTextBrush);
            }

            _ticketInput.Text = string.Empty;
            ClearTicketDetails(preserveCustomerDisplay: true);
            _additionalAmount = 0;
            SyncAdditionalButtons(null);
            if (_paymentMethodComboBox != null) _paymentMethodComboBox.SelectedIndex = 0;
            UpdateServiceTotal();
            LoadCashBox();
        }
        catch (Exception exception)
        {
            ShowError(exception.Message);
        }
        finally
        {
            DispatcherQueue.TryEnqueue(() => _ticketInput.Focus(FocusState.Programmatic));
        }
    }

    private void ShowReadyState()
    {
        CashBoxCustomerDisplayState.Current.SetIdle();
        _amountText.Text = "$0.00";
        _commissionText.Text = "$0.00";
        _serviceReceiptText.Text = "No service";
        _cashTotalText.Text = "$0.00";
        SelectService(null, null);
        ClearTicketDetails();
        if (_paymentMethodComboBox != null) _paymentMethodComboBox.SelectedIndex = 0;
        SetMessage("Waiting for ticket and service.", NeutralTextBrush);
    }

    private void LookupTicket()
    {
        try
        {
            var ticket = _service.LookupTicket(_ticketInput.Text);
            _ticketCustomerText.Text = ticket.CustomerName;
            _ticketBarberText.Text = $"{ticket.BarberStationCode} - {ticket.BarberName}";
            _hasLoadedTicket = true;
            _loadedTicketInput = NormalizeTicketInput(_ticketInput.Text);
            SetServiceOptionsEnabled(true);
            SetMessage($"Ticket {ticket.DisplayTicketNumber} found. Verify customer and barber before completing.", SuccessTextBrush);
            CashBoxCustomerDisplayState.Current.SetTicketLoaded(ticket.DisplayTicketNumber.ToString(), ticket.CustomerName, ticket.BarberName, ticket.BarberStationCode.ToString());
        }
        catch (Exception exception)
        {
            ClearTicketDetails();
            ShowError(exception.Message);
        }
    }

    private void ClearTicketDetails(bool preserveCustomerDisplay = false)
    {
        _ticketCustomerText.Text = "No ticket";
        _ticketBarberText.Text = "No ticket";
        _hasLoadedTicket = false;
        _loadedTicketInput = null;
        SelectService(null, null);
        SetServiceOptionsEnabled(false);
        _additionalAmount = 0;
        SyncAdditionalButtons(null);
        UpdateServiceTotal();
        
        if (!preserveCustomerDisplay)
        {
            CashBoxCustomerDisplayState.Current.SetIdle();
        }
    }

    private void UpdateServiceTotal()
    {
        if (_selectedService is not Service selectedService)
        {
            _amountText.Text = "$0.00";
            _commissionText.Text = "$0.00";
            _cashTotalText.Text = "$0.00";
            
            if (_hasLoadedTicket)
            {
                var barberParts = _ticketBarberText.Text.Split(" - ");
                var station = barberParts.Length > 0 ? barberParts[0].Trim() : "";
                var barber = barberParts.Length > 1 ? barberParts[1].Trim() : "";
                CashBoxCustomerDisplayState.Current.SetTicketLoaded(_loadedTicketInput ?? "", _ticketCustomerText.Text, barber, station);
            }
            return;
        }
        _amountText.Text = $"${selectedService.DesktopPrice:0.00}";
        _commissionText.Text = $"${_additionalAmount:0.00}";
        _cashTotalText.Text = $"${selectedService.DesktopPrice + _additionalAmount:0.00}";
        
        CashBoxCustomerDisplayState.Current.SetServiceSelected(selectedService.Name, selectedService.DesktopPrice, _additionalAmount);
    }

    private void SyncServiceOptions()
    {
        var selectedServiceId = _selectedService?.Id;
        _serviceOptionsGrid.Children.Clear();
        _serviceOptionsGrid.RowDefinitions.Clear();
        _selectedService = null;
        _selectedServiceButton = null;

        if (_services.Count == 0)
        {
            _serviceOptionsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _serviceOptionsGrid.Children.Add(new TextBlock
            {
                Text = "No active services available.",
                Foreground = Brush(117, 118, 135),
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 6, 0, 0)
            });
            return;
        }

        var rows = (_services.Count + ServiceOptionColumnCount - 1) / ServiceOptionColumnCount;
        for (var rowIndex = 0; rowIndex < rows; rowIndex++)
        {
            _serviceOptionsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        for (var index = 0; index < _services.Count; index++)
        {
            var service = _services[index];
            var serviceButton = CreateServiceButton(service);
            serviceButton.IsEnabled = _hasLoadedTicket;
            Grid.SetColumn(serviceButton, index % ServiceOptionColumnCount);
            Grid.SetRow(serviceButton, index / ServiceOptionColumnCount);
            _serviceOptionsGrid.Children.Add(serviceButton);

            if (service.Id == selectedServiceId)
            {
                SelectService(service, serviceButton);
            }
        }
    }

    private ToggleButton CreateServiceButton(Service service)
    {
        var nameText = new TextBlock
        {
            Text = service.Name,
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = NeutralTextBrush,
            TextWrapping = TextWrapping.WrapWholeWords,
            MaxLines = 2
        };

        var priceText = new TextBlock
        {
            Text = $"${service.DesktopPrice:0.00}",
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = Brush(0, 32, 194)
        };

        var content = new StackPanel
        {
            Spacing = 6
        };
        content.Children.Add(nameText);
        content.Children.Add(priceText);

        var button = new ToggleButton
        {
            MinHeight = 64,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(12, 10, 12, 10),
            Background = Brush(249, 249, 252),
            BorderBrush = Brush(226, 226, 229),
            BorderThickness = new Thickness(1),
            Content = content,
            Tag = service
        };
        button.Click += OnServiceOptionClick;
        ToolTipService.SetToolTip(button, service.DisplayNameWithPrice);
        return button;
    }

    private void OnServiceOptionClick(object sender, RoutedEventArgs args)
    {
        if (!_hasLoadedTicket)
        {
            ShowError("Load a ticket before selecting a service.");
            return;
        }

        if (sender is not ToggleButton button || button.Tag is not Service service)
        {
            return;
        }

        SelectService(service, button);
        _additionalAmount = 0;
        SyncAdditionalButtons(null);
        UpdateServiceTotal();
    }

    private void SelectService(Service? service, ToggleButton? selectedButton)
    {
        if (_selectedServiceButton is not null && _selectedServiceButton != selectedButton)
        {
            SetServiceButtonSelected(_selectedServiceButton, false);
        }

        _selectedService = service;
        _selectedServiceButton = selectedButton;

        if (selectedButton is not null)
        {
            SetServiceButtonSelected(selectedButton, true);
        }
    }

    private static void SetServiceButtonSelected(ToggleButton button, bool isSelected)
    {
        button.IsChecked = isSelected;
        button.Background = isSelected ? Brush(235, 240, 255) : Brush(249, 249, 252);
        button.BorderBrush = isSelected ? Brush(0, 32, 194) : Brush(226, 226, 229);
    }

    private void SyncAdditionalButtons(ToggleButton? selectedButton)
    {
        foreach (var button in new[] { _additional2Button, _additional3Button, _additional5Button })
        {
            if (button != selectedButton)
            {
                button.IsChecked = false;
            }
        }
    }

    private void SetServiceOptionsEnabled(bool isEnabled)
    {
        foreach (var child in _serviceOptionsGrid.Children.OfType<ToggleButton>())
        {
            child.IsEnabled = isEnabled;
        }
    }

    private void SetPendingPaymentsButtonText(int count)
    {
        if (_pendingPaymentsButton.Content is StackPanel panel)
        {
            var text = panel.Children.OfType<TextBlock>().FirstOrDefault();
            if (text is not null)
            {
                text.Text = $"Pending Payments ({count})";
                return;
            }
        }

        _pendingPaymentsButton.Content = $"Pending Payments ({count})";
    }

    private static Grid CreatePendingPaymentRowContent(PendingServicePaymentRow row)
    {
        var container = new Grid
        {
            ColumnSpacing = 14,
            Padding = new Thickness(4, 0, 0, 0)
        };
        container.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        container.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var details = new StackPanel
        {
            Spacing = 6
        };

        details.Children.Add(new TextBlock
        {
            Text = $"#{row.DisplayTicketNumber} - {row.CustomerName}",
            FontSize = 19,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = NeutralTextBrush,
            TextWrapping = TextWrapping.WrapWholeWords
        });

        details.Children.Add(new TextBlock
        {
            Text = $"Original barber: {row.BarberStationCode} {row.BarberName}",
            FontSize = 15,
            Foreground = Brush(68, 70, 85),
            TextWrapping = TextWrapping.WrapWholeWords
        });

        details.Children.Add(new TextBlock
        {
            Text = row.AdditionalAmount > 0
                ? $"{row.ServiceName} ${row.ServicePrice:0.00} + ${row.AdditionalAmount:0.00}"
                : row.ServiceName,
            FontSize = 14,
            Foreground = Brush(68, 70, 85),
            TextWrapping = TextWrapping.WrapWholeWords
        });

        var amountText = new TextBlock
        {
            Text = $"${row.Amount:0.00}",
            FontSize = 24,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = Brush(0, 32, 194),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0)
        };

        Grid.SetColumn(details, 0);
        Grid.SetColumn(amountText, 1);
        container.Children.Add(details);
        container.Children.Add(amountText);
        return container;
    }
    private static string NormalizeTicketInput(string value)
    {
        return value.Trim();
    }

    private void ShowError(string message)
    {
        MessageBeep(ErrorBeepType);
        SetMessage(message, ErrorTextBrush);
    }

    private void ApplyResponsiveLayout(double width)
    {
        var useNarrowLayout = width > 0 && width < NarrowLayoutThreshold;

        _screenScrollViewer.Padding = useNarrowLayout
            ? new Thickness(20, 72, 20, 24)
            : new Thickness(48, 48, 48, 32);

        _cashBoxContentGrid.ColumnSpacing = useNarrowLayout ? 0 : 24;
        _cashBoxContentGrid.RowSpacing = useNarrowLayout ? 24 : 0;
        _activeTicketColumn.Width = new GridLength(7, GridUnitType.Star);
        _paymentSummaryColumn.Width = useNarrowLayout
            ? new GridLength(0)
            : new GridLength(3, GridUnitType.Star);
        _paymentSummaryColumn.MinWidth = useNarrowLayout ? 0 : 320;

        Grid.SetColumn(_summaryPanel, useNarrowLayout ? 0 : 1);
        Grid.SetRow(_summaryPanel, useNarrowLayout ? 1 : 0);
    }

    private void SetMessage(string message, SolidColorBrush foreground)
    {
        _messageText.Text = message;
        _messageText.Foreground = foreground;
    }

    private static SolidColorBrush Brush(byte red, byte green, byte blue)
    {
        return new SolidColorBrush(ColorHelper.FromArgb(255, red, green, blue));
    }

    [DllImport("user32.dll")]
    private static extern bool MessageBeep(uint uType);

    private async void OnOpenCashBoxClick(object sender, RoutedEventArgs args)
    {
        var isCorrection = _cashBoxSnapshot?.IsCashBoxOpened == true;

        if (isCorrection)
        {
            var passwordBox = new PasswordBox { PlaceholderText = "Admin password" };
            var authDialog = new ContentDialog
            {
                Title = "Correct Opening Cash",
                Content = passwordBox,
                PrimaryButtonText = "Submit",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot
            };

            var authResult = await authDialog.ShowAsync();
            if (authResult != ContentDialogResult.Primary)
            {
                return;
            }

            if (passwordBox.Password != "G1234")
            {
                ShowError("Invalid password.");
                return;
            }
        }

        var openingInput = new TextBox
        {
            Header = "Opening cash",
            PlaceholderText = "0.00",
            Text = isCorrection && _cashBoxSnapshot is not null ? _cashBoxSnapshot.OpeningBalance.ToString("0.00", CultureInfo.InvariantCulture) : string.Empty,
            MinHeight = 56,
            FontSize = 20,
            Padding = new Thickness(14, 8, 14, 8)
        };
        var statusText = new TextBlock
        {
            Text = "Enter the cash physically placed in the drawer before collecting payments.",
            FontSize = 14,
            Foreground = NeutralTextBrush,
            TextWrapping = TextWrapping.WrapWholeWords
        };
        var content = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                openingInput,
                statusText
            }
        };
        var dialog = new ContentDialog
        {
            Title = isCorrection ? "Correct Opening Cash" : "Open Cash Box",
            Content = content,
            PrimaryButtonText = isCorrection ? "Save Correction" : "Open Cash Box",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        if (!TryParseMoney(openingInput.Text, out var openingBalance))
        {
            ShowError("Enter a valid opening cash amount.");
            return;
        }

        try
        {
            var saveResult = _service.SaveOpeningBalance(openingBalance);
            LoadCashBox();
            SetMessage(
                saveResult.WasCorrection
                    ? $"Opening cash corrected to ${saveResult.OpeningBalance:0.00}."
                    : $"Cash box opened with ${saveResult.OpeningBalance:0.00}.",
                SuccessTextBrush);
        }
        catch (Exception exception)
        {
            ShowError(exception.Message);
        }
        finally
        {
            DispatcherQueue.TryEnqueue(() => _ticketInput.Focus(FocusState.Programmatic));
        }
    }

    private void UpdateCashBoxOpeningSummary(CashBoxSnapshot snapshot)
    {
        _openCashBoxButtonText.Text = snapshot.IsCashBoxOpened ? "Correct Opening Cash" : "Open Cash Box";
        _completeTransactionButton.IsEnabled = snapshot.IsCashBoxOpened;
    }

    private static string FormatMoney(decimal amount)
    {
        return string.Create(CultureInfo.InvariantCulture, $"${amount:0.00}");
    }

    private static bool TryParseMoney(string value, out decimal amount)
    {
        var normalized = value.Trim();
        return decimal.TryParse(normalized, NumberStyles.Currency, CultureInfo.CurrentCulture, out amount)
            || decimal.TryParse(normalized, NumberStyles.Currency, CultureInfo.InvariantCulture, out amount);
    }
}
