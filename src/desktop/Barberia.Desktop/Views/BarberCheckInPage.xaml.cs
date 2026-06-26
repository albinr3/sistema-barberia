using Barberia.Core.Domain;
using Barberia.Data.Models;
using Barberia.Desktop.Services;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;

namespace Barberia.Desktop.Views;

public sealed partial class BarberCheckInPage : Page
{
    private readonly BarberCheckInService _service = new();
    private IReadOnlyDictionary<Guid, DailyRotationEntry> _dailyRotationEntries = new Dictionary<Guid, DailyRotationEntry>();

    public event EventHandler? ShellMenuRequested;

    public BarberCheckInPage()
    {
        InitializeComponent();
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        LoadData();
        QueueStationFocus();
    }

    private void OnMenuButtonClick(object sender, RoutedEventArgs args)
    {
        ShellMenuRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnCheckInClick(object sender, RoutedEventArgs args)
    {
        CheckIn();
    }

    private void OnStationInputKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key == VirtualKey.Enter)
        {
            CheckIn();
            args.Handled = true;
        }
    }

    private void CheckIn()
    {
        try
        {
            var result = _service.CheckIn(_stationInput.Text);
            _stationInput.Text = string.Empty;
            var assignedText = result.AssignedDisplayTicketNumber is int displayTicketNumber
                ? $" Next ticket called: {displayTicketNumber}."
                : string.Empty;
            SetStatus($"{result.BarberStationCode} - {result.BarberName} checked in as #{result.QueuePosition}.{assignedText}", success: true);
            LoadData();
        }
        catch (Exception exception)
        {
            SetStatus(exception.Message, success: false);
        }
        finally
        {
            QueueStationFocus();
        }
    }

    private void LoadData()
    {
        try
        {
            var snapshot = _service.Load();
            _dailyRotationEntries = snapshot.DailyRotationEntries.ToDictionary(entry => entry.BarberId);
            RenderRotation(snapshot.Barbers);
        }
        catch (Exception exception)
        {
            SetStatus(exception.Message, success: false);
        }
    }

    private void RenderRotation(IReadOnlyList<Barber> barbers)
    {
        _rotationPanel.Children.Clear();

        if (barbers.Count == 0)
        {
            _rotationPanel.Children.Add(new TextBlock
            {
                Padding = new Thickness(16),
                FontFamily = new FontFamily("Inter"),
                FontSize = 14,
                Foreground = Brush(68, 70, 85),
                Text = "No active barbers are configured."
            });
            return;
        }

        foreach (var barber in barbers)
        {
            _rotationPanel.Children.Add(CreateRotationRow(barber));
        }
    }

    private FrameworkElement CreateRotationRow(Barber barber)
    {
        _dailyRotationEntries.TryGetValue(barber.Id, out var entry);

        var container = new Border
        {
            BorderBrush = Brush(226, 226, 229),
            BorderThickness = new Thickness(0, 0, 0, 1)
        };

        var row = new Grid
        {
            Padding = new Thickness(16, 14, 16, 14),
            ColumnSpacing = 16
        };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(72) });
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });

        var position = new Border
        {
            Width = 48,
            Height = 36,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Background = entry is null ? Brush(243, 243, 246) : Brush(223, 224, 255),
            BorderBrush = entry is null ? Brush(226, 226, 229) : Brush(0, 19, 135),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Child = new TextBlock
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = new FontFamily("Inter"),
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = entry is null ? Brush(68, 70, 85) : Brush(0, 11, 98),
                Text = entry is null ? "--" : $"#{entry.QueuePosition + 1}"
            }
        };
        row.Children.Add(position);

        var details = new StackPanel
        {
            Spacing = 4
        };
        Grid.SetColumn(details, 1);
        details.Children.Add(new TextBlock
        {
            FontFamily = new FontFamily("Inter"),
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush(26, 28, 30),
            Text = barber.DisplayNameWithStation,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        details.Children.Add(new TextBlock
        {
            FontFamily = new FontFamily("Inter"),
            FontSize = 13,
            Foreground = Brush(68, 70, 85),
            Text = entry is null
                ? "Not checked in today"
                : $"Checked in at {entry.ArrivedAt:hh:mm tt}",
            TextWrapping = TextWrapping.WrapWholeWords
        });
        row.Children.Add(details);

        var state = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            FontFamily = new FontFamily("Inter"),
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = GetStateBrush(barber.State),
            Text = FormatState(barber.State),
            TextAlignment = TextAlignment.Right,
            TextWrapping = TextWrapping.WrapWholeWords
        };
        Grid.SetColumn(state, 2);
        row.Children.Add(state);

        container.Child = row;
        return container;
    }

    private void QueueStationFocus()
    {
        DispatcherQueue.TryEnqueue(() => _stationInput.Focus(FocusState.Programmatic));
    }

    private void SetStatus(string text, bool success)
    {
        _messageText.Text = text;
        _messageText.Foreground = success ? Brush(17, 105, 88) : Brush(154, 58, 47);
    }

    private static string FormatState(BarberState state) => state switch
    {
        BarberState.Available => "Available",
        BarberState.NotCheckedIn => "Selectable",
        BarberState.Offline => "Offline",
        BarberState.Called => "Called",
        BarberState.InService => "In service",
        _ => state.ToString()
    };

    private static SolidColorBrush GetStateBrush(BarberState state) => state switch
    {
        BarberState.Available => Brush(17, 105, 88),
        BarberState.NotCheckedIn => Brush(68, 70, 85),
        BarberState.Offline => Brush(147, 0, 10),
        BarberState.Called => Brush(0, 32, 194),
        BarberState.InService => Brush(101, 65, 0),
        _ => Brush(68, 70, 85)
    };

    private static SolidColorBrush Brush(byte red, byte green, byte blue)
    {
        return new SolidColorBrush(ColorHelper.FromArgb(255, red, green, blue));
    }
}