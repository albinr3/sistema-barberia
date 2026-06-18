using Barberia.Data.Models;
using Barberia.Desktop.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Linq;

namespace Barberia.Desktop.Views;

public sealed partial class ReceiptReprintWindow : Window
{
    private readonly ReceiptReprintService _service = new();

    public ReceiptReprintWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        
        var now = OperationalClock.Now;
        _datePicker.Date = now.Date;
        
        LoadReceipts();
    }

    private void OnDateChanged(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args)
    {
        LoadReceipts();
    }

    private void OnSearchKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            LoadReceipts();
            e.Handled = true;
        }
    }

    private void OnSearchClick(object sender, RoutedEventArgs e)
    {
        LoadReceipts();
    }

    private void LoadReceipts()
    {
        try
        {
            var date = _datePicker.Date?.Date ?? OperationalClock.Now.Date;
            var offset = OperationalClock.Now.Offset;
            var businessDate = new DateTimeOffset(date, offset);
            var query = _searchInput.Text;
            
            var receipts = _service.SearchReceipts(businessDate, query);
            _receiptsList.ItemsSource = receipts.ToList();
            
            SetMessage($"Loaded {receipts.Count} receipts.", new SolidColorBrush(Microsoft.UI.Colors.Green));
        }
        catch (Exception ex)
        {
            SetMessage($"Error: {ex.Message}", new SolidColorBrush(Microsoft.UI.Colors.Red));
        }
    }

    private void OnReprintItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is ReceiptPrintRecord record)
        {
            try
            {
                _service.ReprintReceipt(record);
                SetMessage($"Successfully reprinted receipt {record.ReceiptNumber}.", new SolidColorBrush(Microsoft.UI.Colors.Green));
            }
            catch (Exception ex)
            {
                SetMessage($"Error reprinting: {ex.Message}", new SolidColorBrush(Microsoft.UI.Colors.Red));
            }
        }
    }

    private void SetMessage(string message, SolidColorBrush color)
    {
        _messageText.Text = message;
        _messageText.Foreground = color;
    }
}
