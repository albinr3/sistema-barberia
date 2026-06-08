using Barberia.Data.Models;
using Barberia.Desktop.Services;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace Barberia.Desktop.Views;

public sealed partial class CashBoxPage : Page
{
    private const double NarrowLayoutThreshold = 900;
    private const int ServiceOptionColumnCount = 3;

    private static readonly SolidColorBrush SuccessTextBrush = Brush(17, 105, 88);
    private static readonly SolidColorBrush ErrorTextBrush = Brush(154, 58, 47);
    private static readonly SolidColorBrush NeutralTextBrush = Brush(26, 28, 30);

    private readonly CashBoxCloseService _service = new();
    private readonly MediaPlayer _successPlayer;
    private IReadOnlyList<Service> _services = [];
    private Service? _selectedService;
    private ToggleButton? _selectedServiceButton;
    private decimal _additionalAmount;

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
    }

    private void OnCloseClick(object sender, RoutedEventArgs args)
    {
        CloseService();
    }

    private void OnMenuButtonClick(object sender, RoutedEventArgs args)
    {
        ShellMenuRequested?.Invoke(this, EventArgs.Empty);
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

    private void LoadCashBox()
    {
        try
        {
            var snapshot = _service.Load();
            _services = snapshot.Services;
            SyncServiceOptions();
            _lastRefreshText.Text = $"Updated: {snapshot.LoadedAt:hh:mm tt}";
            SetMessage("Waiting for ticket and service.", SuccessTextBrush);
            UpdateServiceTotal();
        }
        catch (Exception exception)
        {
            ShowError(exception.Message);
        }
    }

    private void CloseService()
    {
        if (_selectedService is not Service selectedService)
        {
            ShowError("Select a service.");
            DispatcherQueue.TryEnqueue(() => _ticketInput.Focus(FocusState.Programmatic));
            return;
        }

        try
        {
            var result = _service.CloseService(_ticketInput.Text, selectedService.Id, _additionalAmount);

            _serviceReceiptText.Text = result.AdditionalAmount > 0
                ? $"{result.ServiceName} {result.ServicePrice:0.00} + addition {result.AdditionalAmount:0.00}"
                : $"{result.ServiceName} {result.ServicePrice:0.00}";
            _successPlayer.Play();
            SetMessage($"{result.DisplayTicketNumber} - {result.BarberStationCode} - {result.Message}", SuccessTextBrush);
            _ticketInput.Text = string.Empty;
            ClearTicketDetails();
            SelectService(null, null);
            _additionalAmount = 0;
            SyncAdditionalButtons(null);
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
        _amountText.Text = "$0.00";
        _commissionText.Text = "$0.00";
        _serviceReceiptText.Text = "No service";
        _cashTotalText.Text = "$0.00";
        SelectService(null, null);
        ClearTicketDetails();
        SetMessage("Waiting for ticket and service.", NeutralTextBrush);
    }

    private void LookupTicket()
    {
        try
        {
            var ticket = _service.LookupTicket(_ticketInput.Text);
            _ticketCustomerText.Text = ticket.CustomerName;
            _ticketBarberText.Text = $"{ticket.BarberStationCode} - {ticket.BarberName}";
            SetMessage($"Ticket {ticket.DisplayTicketNumber} found. Verify customer and barber before completing.", SuccessTextBrush);
        }
        catch (Exception exception)
        {
            ClearTicketDetails();
            ShowError(exception.Message);
        }
    }

    private void ClearTicketDetails()
    {
        _ticketCustomerText.Text = "No ticket";
        _ticketBarberText.Text = "No ticket";
    }

    private void UpdateServiceTotal()
    {
        if (_selectedService is not Service selectedService)
        {
            _amountText.Text = "$0.00";
            _commissionText.Text = "$0.00";
            _cashTotalText.Text = "$0.00";
            return;
        }
        _amountText.Text = $"${selectedService.Price:0.00}";
        _commissionText.Text = $"${_additionalAmount:0.00}";
        _cashTotalText.Text = $"${selectedService.Price + _additionalAmount:0.00}";
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
            Text = $"${service.Price:0.00}",
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

    private void ShowError(string message)
    {
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
}
