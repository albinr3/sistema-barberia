using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Globalization;
using Windows.System;
using Barberia.Desktop.Services;

namespace Barberia.Desktop.Views;

public sealed partial class ChangeCalculationDialog : ContentDialog
{
    private readonly decimal _totalAmount;
    private decimal _tenderedAmount;
    private decimal _changeAmount;

    public decimal TenderedAmount => _tenderedAmount;
    public decimal ChangeAmount => _changeAmount;

    public ChangeCalculationDialog(decimal totalAmount)
    {
        InitializeComponent();
        _totalAmount = totalAmount;
        _totalAmountText.Text = $"${_totalAmount:0.00}";
        UpdateCalculation();
    }

    private void ContentDialog_Opened(ContentDialog sender, ContentDialogOpenedEventArgs args)
    {
        _tenderedInput.Focus(FocusState.Programmatic);
    }

    private void OnTenderedInputTextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateCalculation();
    }

    private void OnTenderedInputKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            if (IsPrimaryButtonEnabled)
            {
                Hide();
            }
            e.Handled = true;
        }
    }

    private void OnQuickButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string tag)
        {
            if (tag == "Exact")
            {
                _tenderedInput.Text = _totalAmount.ToString("0.00", CultureInfo.InvariantCulture);
            }
            else if (decimal.TryParse(tag, out var quickAmount))
            {
                _tenderedInput.Text = quickAmount.ToString("0.00", CultureInfo.InvariantCulture);
            }
        }
    }

    private void UpdateCalculation()
    {
        if (string.IsNullOrWhiteSpace(_tenderedInput.Text))
        {
            _tenderedAmount = 0;
            _changeAmount = 0;
            SetStatus(false, "$0.00");
            return;
        }

        if (decimal.TryParse(_tenderedInput.Text, out var tendered))
        {
            _tenderedAmount = tendered;
            _changeAmount = _tenderedAmount - _totalAmount;

            if (_changeAmount < 0)
            {
                SetStatus(false, $"Insufficient: -${Math.Abs(_changeAmount):0.00}");
            }
            else
            {
                SetStatus(true, $"${_changeAmount:0.00}");
            }
        }
        else
        {
            _tenderedAmount = 0;
            _changeAmount = 0;
            SetStatus(false, "Invalid amount");
        }
        
        CashBoxCustomerDisplayState.Current.UpdateCashPayment(_tenderedAmount, Math.Max(0, _changeAmount));
    }

    private void SetStatus(bool isValid, string changeText)
    {
        IsPrimaryButtonEnabled = isValid;
        _changeAmountText.Text = changeText;

        if (isValid)
        {
            _changeAmountText.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 17, 105, 88)); // Success Green
            _validationMessage.Visibility = Visibility.Collapsed;
        }
        else
        {
            _changeAmountText.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 154, 58, 47)); // Error Red
            if (_tenderedAmount > 0)
            {
                _validationMessage.Text = "Tendered amount must be greater than or equal to total.";
                _validationMessage.Visibility = Visibility.Visible;
            }
            else
            {
                _validationMessage.Visibility = Visibility.Collapsed;
            }
        }
    }
}
