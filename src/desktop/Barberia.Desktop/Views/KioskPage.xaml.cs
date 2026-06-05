using Barberia.Core.Domain;
using Barberia.Desktop.Services;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using Windows.System;
using Windows.UI.ViewManagement;

namespace Barberia.Desktop.Views;

public sealed partial class KioskPage : Page
{
    private readonly KioskCheckInService _checkInService = new();
    private readonly Dictionary<Guid, Button> _barberButtons = [];
    private readonly HashSet<Guid> _selectedBarberIds = [];
    private IReadOnlyList<Barber> _barbers = [];
    private bool _acceptsAnyBarber = true;
    private string _customerName = string.Empty;

    public event EventHandler? ShellMenuRequested;

    public KioskPage()
    {
        InitializeComponent();
        LoadBrandLogo();
        LoadBarbers();
        RenderBarberChoices();
        UpdateInteractionState();
    }

    private void LoadBrandLogo()
    {
        var logoPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "logo (2).png");
        if (!System.IO.File.Exists(logoPath))
        {
            _brandFallbackText.Visibility = Visibility.Visible;
            return;
        }

        _brandLogo.Source = new BitmapImage(new Uri(logoPath));
    }

    private void OnHelpButtonClick(object sender, RoutedEventArgs args)
    {
        ShellMenuRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnCustomerNameFocus(object sender, RoutedEventArgs args)
    {
        ShowTouchKeyboard();
    }

    private void OnCustomerNameTapped(object sender, TappedRoutedEventArgs args)
    {
        ShowTouchKeyboard();
    }

    private static void ShowTouchKeyboard()
    {
        try
        {
            if (InputPane.GetForCurrentView().TryShow())
            {
                return;
            }
        }
        catch (InvalidOperationException)
        {
        }
        catch (COMException)
        {
        }

        TryStartTouchKeyboardProcess();
    }

    private static void TryStartTouchKeyboardProcess()
    {
        var tabTipPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles),
            "Microsoft Shared",
            "ink",
            "TabTip.exe");

        if (!System.IO.File.Exists(tabTipPath))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = tabTipPath,
                UseShellExecute = true
            });
        }
        catch (InvalidOperationException)
        {
        }
        catch (Win32Exception)
        {
        }
        catch (COMException)
        {
        }
    }

    private void OnCustomerNameChanged(object sender, TextChangedEventArgs args)
    {
        _customerName = _customerNameInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(_customerName))
        {
            ResetToAnyBarber();
        }

        _nameErrorText.Visibility = Visibility.Collapsed;
        UpdateInteractionState();
    }

    private void OnCustomerNameKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key == VirtualKey.Enter)
        {
            args.Handled = true;
            Focus(FocusState.Programmatic);
        }
    }

    private void OnAnyBarberButtonClick(object sender, RoutedEventArgs args)
    {
        if (!CanSelectBarbers)
        {
            return;
        }

        _acceptsAnyBarber = true;
        _selectedBarberIds.Clear();
        UpdateSelectionVisuals();
        UpdateInteractionState();
    }

    private void OnPrintTicketButtonClick(object sender, RoutedEventArgs args)
    {
        PrintTicket();
    }

    private void OnStartNewButtonClick(object sender, RoutedEventArgs args)
    {
        _customerNameInput.Text = string.Empty;
        _customerName = string.Empty;
        ResetToAnyBarber();
        _checkInPanel.Visibility = Visibility.Visible;
        _ticketPanel.Visibility = Visibility.Collapsed;
        _customerNameInput.Focus(FocusState.Programmatic);
        UpdateInteractionState();
    }

    private void PrintTicket()
    {
        if (string.IsNullOrWhiteSpace(_customerName))
        {
            _nameErrorText.Text = "Enter your name before printing.";
            _nameErrorText.Visibility = Visibility.Visible;
            return;
        }

        _printTicketButton.IsEnabled = false;
        _barberErrorText.Visibility = Visibility.Collapsed;

        try
        {
            var result = _checkInService.RegisterWalkIn(
                _customerName,
                _acceptsAnyBarber,
                _selectedBarberIds.ToArray());
            ShowPrintedTicket(result);
            LoadBarbers();
            RenderBarberChoices();
        }
        catch (Exception exception)
        {
            _barberErrorText.Text = exception.Message;
            _barberErrorText.Visibility = Visibility.Visible;
        }
        finally
        {
            if (_ticketPanel.Visibility != Visibility.Visible)
            {
                UpdateInteractionState();
            }
        }
    }

    private void LoadBarbers()
    {
        try
        {
            _barbers = _checkInService.Load().Barbers;
        }
        catch (Exception exception)
        {
            _barbers = [];
            _barberErrorText.Text = exception.Message;
            _barberErrorText.Visibility = Visibility.Visible;
        }
    }

    private void RenderBarberChoices()
    {
        _barberGrid.Children.Clear();
        _barberGrid.ColumnDefinitions.Clear();
        _barberGrid.RowDefinitions.Clear();
        _barberButtons.Clear();

        for (var column = 0; column < 4; column++)
        {
            _barberGrid.ColumnDefinitions.Add(new ColumnDefinition());
        }

        if (_barbers.Count == 0)
        {
            _barberGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var emptyState = CreateEmptyState();
            Grid.SetColumnSpan(emptyState, 4);
            _barberGrid.Children.Add(emptyState);
            return;
        }

        for (var index = 0; index < _barbers.Count; index++)
        {
            if (index % 4 == 0)
            {
                _barberGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }

            var barber = _barbers[index];
            var button = CreateBarberCard(barber);
            _barberButtons[barber.Id] = button;
            Grid.SetRow(button, index / 4);
            Grid.SetColumn(button, index % 4);
            _barberGrid.Children.Add(button);
        }

        UpdateInteractionState();
        UpdateSelectionVisuals();
    }

    private Button CreateBarberCard(Barber barber)
    {
        var button = new Button
        {
            MinHeight = 224,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(22),
            Background = Brush(255, 255, 255),
            BorderBrush = Brush(233, 236, 239),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Tag = barber.Id,
            Content = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = 12,
                Children =
                {
                    CreateAvatar(barber),
                    new TextBlock
                    {
                        Text = barber.DisplayName,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        FontSize = 20,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = Brush(26, 28, 30),
                        TextAlignment = TextAlignment.Center,
                        TextWrapping = TextWrapping.WrapWholeWords
                    },
                    CreateStatusBadge(barber)
                }
            }
        };

        button.Click += (_, _) =>
        {
            if (!CanSelectBarbers || !IsSelectable(barber))
            {
                return;
            }

            _acceptsAnyBarber = false;
            if (!_selectedBarberIds.Add(barber.Id))
            {
                _selectedBarberIds.Remove(barber.Id);
            }

            UpdateSelectionVisuals();
            UpdateInteractionState();
        };

        return button;
    }

    private static Grid CreateAvatar(Barber barber)
    {
        var accent = GetStateColor(barber.State);
        var avatar = new Grid
        {
            Width = 96,
            Height = 96,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        avatar.Children.Add(new Ellipse
        {
            Fill = Brush(243, 243, 246),
            Stroke = Brush(226, 226, 229),
            StrokeThickness = 1
        });

        avatar.Children.Add(new TextBlock
        {
            Text = GetInitials(barber.DisplayName),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 30,
            FontWeight = FontWeights.Bold,
            Foreground = Brush(0, 19, 135)
        });

        var imageUri = ProfileImageCatalog.ResolveImageUri(barber.ProfileImagePath);
        if (imageUri is not null)
        {
            avatar.Children.Add(new Ellipse
            {
                Fill = new ImageBrush
                {
                    ImageSource = new BitmapImage(imageUri),
                    Stretch = Stretch.UniformToFill
                },
                Stroke = Brush(255, 255, 255),
                StrokeThickness = 2
            });
        }

        var statusDot = new Ellipse
        {
            Width = 16,
            Height = 16,
            Fill = accent,
            Stroke = Brush(255, 255, 255),
            StrokeThickness = 2,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom
        };
        avatar.Children.Add(statusDot);

        return avatar;
    }

    private static Border CreateStatusBadge(Barber barber)
    {
        var (text, background, foreground) = GetStatusBadge(barber);

        return new Border
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Background = background,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 4, 8, 4),
            Child = new TextBlock
            {
                Text = text,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = foreground
            }
        };
    }

    private Border CreateEmptyState()
    {
        return new Border
        {
            Background = Brush(255, 218, 214),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(20),
            Child = new TextBlock
            {
                Text = "No barbers are available in the local database.",
                FontSize = 16,
                Foreground = Brush(147, 0, 10),
                TextWrapping = TextWrapping.Wrap
            }
        };
    }

    private void UpdateInteractionState()
    {
        var canSelect = CanSelectBarbers;
        _anyBarberButton.IsEnabled = canSelect;
        _anyBarberButton.Opacity = canSelect ? 1 : 0.5;

        foreach (var barber in _barbers)
        {
            if (_barberButtons.TryGetValue(barber.Id, out var button))
            {
                var canSelectBarber = canSelect && IsSelectable(barber);
                button.IsEnabled = canSelectBarber;
                button.Opacity = canSelectBarber ? 1 : 0.5;
            }
        }

        var hasSelection = _acceptsAnyBarber || _selectedBarberIds.Count > 0;
        _printTicketButton.IsEnabled = canSelect && hasSelection;
        _selectionSummaryText.Text = canSelect
            ? hasSelection ? FormatSelectionSummary() : "Choose a barber"
            : "Enter name first";
    }

    private void UpdateSelectionVisuals()
    {
        ApplyAnyBarberVisual();

        foreach (var barber in _barbers)
        {
            if (_barberButtons.TryGetValue(barber.Id, out var button))
            {
                ApplyCardVisual(button, _selectedBarberIds.Contains(barber.Id));
            }
        }
    }

    private void ApplyAnyBarberVisual()
    {
        _anyBarberButton.Background = _acceptsAnyBarber ? Brush(243, 243, 246) : Brush(255, 255, 255);
        _anyBarberButton.BorderBrush = _acceptsAnyBarber ? Brush(0, 32, 194) : Brush(233, 236, 239);
        _anyBarberButton.BorderThickness = _acceptsAnyBarber ? new Thickness(2) : new Thickness(1.5);
    }

    private static void ApplyCardVisual(Button button, bool isSelected)
    {
        button.Background = isSelected ? Brush(243, 243, 246) : Brush(255, 255, 255);
        button.BorderBrush = isSelected ? Brush(0, 32, 194) : Brush(233, 236, 239);
        button.BorderThickness = isSelected ? new Thickness(2) : new Thickness(1);
    }

    private string FormatSelectionSummary()
    {
        if (_acceptsAnyBarber)
        {
            return "Any Barber";
        }

        return string.Join(
            ", ",
            _barbers
                .Where(barber => _selectedBarberIds.Contains(barber.Id))
                .Select(barber => barber.DisplayName));
    }

    private void ResetToAnyBarber()
    {
        _acceptsAnyBarber = true;
        _selectedBarberIds.Clear();
        UpdateSelectionVisuals();
    }

    private void ShowPrintedTicket(KioskCheckInResult result)
    {
        _ticketNumberText.Text = result.TicketNumber;
        _ticketCustomerText.Text = result.CustomerName;
        _ticketBarberText.Text = result.AssignedBarberName
            ?? (result.AcceptsAnyBarber ? "Any Barber" : string.Join(", ", result.RequestedBarberNames));
        _ticketMessageText.Text = result.Message;
        _ticketTimeText.Text = result.CheckedInAt.ToString("hh:mm tt");
        _checkInPanel.Visibility = Visibility.Collapsed;
        _ticketPanel.Visibility = Visibility.Visible;
    }

    private bool CanSelectBarbers => !string.IsNullOrWhiteSpace(_customerName);

    private static bool IsSelectable(Barber barber)
    {
        return barber.State == BarberState.Available;
    }

    private static (string Text, SolidColorBrush Background, SolidColorBrush Foreground) GetStatusBadge(Barber barber)
    {
        return barber.State switch
        {
            BarberState.Available => ("AVAILABLE", Brush(230, 244, 234), Brush(19, 115, 51)),
            BarberState.Called => ("OCCUPIED", Brush(254, 247, 224), Brush(176, 96, 0)),
            BarberState.InService => ("BUSY", Brush(255, 218, 214), Brush(147, 0, 10)),
            BarberState.Offline => ("OFFLINE", Brush(255, 218, 214), Brush(147, 0, 10)),
            _ => ("LOCKED", Brush(243, 243, 246), Brush(68, 70, 85))
        };
    }

    private static SolidColorBrush GetStateColor(BarberState state)
    {
        return state switch
        {
            BarberState.Available => Brush(34, 197, 94),
            BarberState.Called => Brush(245, 158, 11),
            BarberState.InService or BarberState.Offline => Brush(239, 68, 68),
            _ => Brush(117, 118, 135)
        };
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

    private static SolidColorBrush Brush(byte red, byte green, byte blue)
    {
        return new SolidColorBrush(ColorHelper.FromArgb(255, red, green, blue));
    }
}
