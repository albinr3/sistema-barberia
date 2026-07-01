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
    private readonly IKioskStationService _checkInService = StationServiceFactory.CreateKioskService();
    private readonly Dictionary<Guid, Button> _barberButtons = [];
    private readonly HashSet<Guid> _selectedBarberIds = [];
    private int _barberColumnCount = 4;
    private bool _usesDenseDesktopLayout;
    private double _barberCardMinHeight = 132;
    private double _barberCardPadding = 12;
    private double _barberCardSpacing = 6;
    private double _avatarSize = 92;
    private double _avatarFontSize = 26;
    private double _barberNameFontSize = 20;
    private IReadOnlyList<Barber> _barbers = [];
    private bool _acceptsAnyBarber = true;
    private string _customerName = string.Empty;
    private readonly DispatcherTimer _refreshTimer = new();
    private static readonly TimeSpan BarberRefreshInterval = TimeSpan.FromSeconds(5);

    public event EventHandler? ShellMenuRequested;

    public KioskPage()
    {
        InitializeComponent();
        LoadBrandLogo();
        LoadBarbers();
        RenderBarberChoices();
        UpdateInteractionState();

        _refreshTimer.Interval = BarberRefreshInterval;
        _refreshTimer.Tick += (_, _) => RefreshBarbersIfChanged();
    }

    private void OnKioskLoaded(object sender, RoutedEventArgs args)
    {
        Focus(FocusState.Programmatic);
        RefreshBarbersIfChanged();
        _refreshTimer.Start();
    }

    private void OnKioskUnloaded(object sender, RoutedEventArgs args)
    {
        _refreshTimer.Stop();
    }

    private void OnKioskSizeChanged(object sender, SizeChangedEventArgs args)
    {
        UpdateKioskResponsiveLayout(args.NewSize.Width, args.NewSize.Height);
    }

    private void UpdateKioskResponsiveLayout(double width, double height)
    {
        if (width <= 0)
        {
            return;
        }

        _contentCanvas.Width = width;
        var availableHeight = Math.Max(1, height - _topBar.ActualHeight);

        var compact = width < 760;
        var medium = width < 1080;
        var denseDesktop = width >= 1180 && height >= 700;
        var edgePadding = compact ? 16 : medium ? 22 : 28;
        var verticalPadding = denseDesktop ? 8 : compact ? 14 : medium ? 16 : 12;
        var panelPadding = denseDesktop ? 16 : compact ? 18 : medium ? 22 : 20;

        _topBar.MinHeight = denseDesktop ? 50 : compact ? 58 : 54;
        _topBar.Padding = new Thickness(edgePadding, 0, edgePadding, 0);
        _contentCanvas.Padding = new Thickness(edgePadding, verticalPadding, edgePadding, verticalPadding);
        _contentCanvas.MinHeight = availableHeight;
        _contentCanvas.Height = denseDesktop ? availableHeight : double.NaN;
        _screenScrollViewer.VerticalScrollBarVisibility = denseDesktop ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;
        _screenScrollViewer.VerticalScrollMode = denseDesktop ? ScrollMode.Disabled : ScrollMode.Auto;
        _checkInPanel.Padding = new Thickness(panelPadding);
        _checkInLayout.RowSpacing = denseDesktop ? 8 : compact ? 12 : 10;
        _quickControlsGrid.ColumnSpacing = denseDesktop ? 14 : 12;
        _barberSectionGrid.RowSpacing = denseDesktop ? 6 : 8;
        _barberGrid.ColumnSpacing = denseDesktop ? 8 : compact ? 8 : 10;
        _barberGrid.RowSpacing = denseDesktop ? 8 : compact ? 8 : 10;
        _nameInputSection.MaxWidth = compact ? double.PositiveInfinity : 900;
        _titleText.FontSize = denseDesktop ? 30 : compact ? 30 : 34;
        _subtitleText.FontSize = denseDesktop ? 14 : compact ? 13 : 15;
        _brandLogo.Height = denseDesktop ? 32 : 38;
        _brandLogo.MaxWidth = denseDesktop ? 190 : 230;

        Grid.SetColumn(_anyBarberButton, compact ? 0 : 1);
        Grid.SetRow(_anyBarberButton, compact ? 1 : 0);
        Grid.SetColumnSpan(_nameInputSection, compact ? 2 : 1);
        Grid.SetColumnSpan(_anyBarberButton, compact ? 2 : 1);

        if (compact)
        {
            _anyBarberButton.VerticalAlignment = VerticalAlignment.Stretch;
            _printTicketButton.HorizontalAlignment = HorizontalAlignment.Stretch;
        }
        else
        {
            _anyBarberButton.VerticalAlignment = VerticalAlignment.Bottom;
            _printTicketButton.HorizontalAlignment = HorizontalAlignment.Center;
        }

        var nextColumnCount = width < 680 ? 1 : width < 940 ? 2 : width < 1120 ? 3 : width < 1460 ? 4 : 5;
        var metricsChanged = ApplyBarberCardMetrics(denseDesktop, compact);
        if (_barberColumnCount == nextColumnCount && _usesDenseDesktopLayout == denseDesktop && !metricsChanged)
        {
            return;
        }

        _barberColumnCount = nextColumnCount;
        _usesDenseDesktopLayout = denseDesktop;
        RenderBarberChoices();
    }

    private bool ApplyBarberCardMetrics(bool denseDesktop, bool compact)
    {
        var minHeight = denseDesktop ? 148 : compact ? 160 : 176;
        var padding = denseDesktop ? 8 : compact ? 10 : 12;
        var spacing = denseDesktop ? 6 : compact ? 7 : 8;
        var avatarSize = denseDesktop ? 84 : compact ? 94 : 106;
        var avatarFontSize = denseDesktop ? 24 : compact ? 26 : 29;
        var nameFontSize = denseDesktop ? 18 : compact ? 19 : 21;

        var changed = _barberCardMinHeight != minHeight ||
            _barberCardPadding != padding ||
            _barberCardSpacing != spacing ||
            _avatarSize != avatarSize ||
            _avatarFontSize != avatarFontSize ||
            _barberNameFontSize != nameFontSize;

        _barberCardMinHeight = minHeight;
        _barberCardPadding = padding;
        _barberCardSpacing = spacing;
        _avatarSize = avatarSize;
        _avatarFontSize = avatarFontSize;
        _barberNameFontSize = nameFontSize;

        return changed;
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
        ResetCheckInForm();
    }

    private void PrintTicket()
    {
        _printTicketButton.IsEnabled = false;
        _barberErrorText.Visibility = Visibility.Collapsed;

        try
        {
            _checkInService.RegisterWalkIn(
                _customerName,
                _acceptsAnyBarber,
                _selectedBarberIds.ToArray());
            LoadBarbers();
            ResetCheckInForm();
            RenderBarberChoices();
        }
        catch (Exception exception)
        {
            _barberErrorText.Text = exception.Message;
            _barberErrorText.Visibility = Visibility.Visible;
        }
        finally
        {
            UpdateInteractionState();
        }
    }

    private void LoadBarbers()
    {
        try
        {
            _barbers = _checkInService.Load().Barbers
                .OrderBy(barber => barber.StationNumber ?? int.MaxValue)
                .ThenBy(barber => barber.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (Exception exception)
        {
            _barbers = [];
            _barberErrorText.Text = exception.Message;
            _barberErrorText.Visibility = Visibility.Visible;
        }
    }

    private void RefreshBarbersIfChanged()
    {
        try
        {
            var newBarbers = _checkInService.Load().Barbers
                .OrderBy(barber => barber.StationNumber ?? int.MaxValue)
                .ThenBy(barber => barber.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var changed = false;
            if (_barbers.Count != newBarbers.Length)
            {
                changed = true;
            }
            else
            {
                for (int i = 0; i < _barbers.Count; i++)
                {
                    if (_barbers[i].Id != newBarbers[i].Id ||
                        _barbers[i].State != newBarbers[i].State ||
                        _barbers[i].DisplayName != newBarbers[i].DisplayName ||
                        _barbers[i].StationCode != newBarbers[i].StationCode ||
                        _barbers[i].ProfileImagePath != newBarbers[i].ProfileImagePath)
                    {
                        changed = true;
                        break;
                    }
                }
            }

            if (changed)
            {
                _barbers = newBarbers;
                
                var previousSelectionCount = _selectedBarberIds.Count;
                _selectedBarberIds.RemoveWhere(id => 
                {
                    var b = newBarbers.FirstOrDefault(x => x.Id == id);
                    return b == null || !IsSelectable(b);
                });

                if (_selectedBarberIds.Count == 0 && previousSelectionCount > 0)
                {
                    _acceptsAnyBarber = true;
                }

                RenderBarberChoices();
            }
        }
        catch
        {
            // Ignore background load errors
        }
    }

    private void RenderBarberChoices()
    {
        _barberGrid.Children.Clear();
        _barberGrid.ColumnDefinitions.Clear();
        _barberGrid.RowDefinitions.Clear();
        _barberButtons.Clear();

        for (var column = 0; column < _barberColumnCount; column++)
        {
            _barberGrid.ColumnDefinitions.Add(new ColumnDefinition());
        }

        if (_barbers.Count == 0)
        {
            _barberGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var emptyState = CreateEmptyState();
            Grid.SetColumnSpan(emptyState, _barberColumnCount);
            _barberGrid.Children.Add(emptyState);
            return;
        }

        for (var index = 0; index < _barbers.Count; index++)
        {
            if (index % _barberColumnCount == 0)
            {
                _barberGrid.RowDefinitions.Add(new RowDefinition
                {
                    Height = _usesDenseDesktopLayout ? new GridLength(1, GridUnitType.Star) : GridLength.Auto
                });
            }

            var barber = _barbers[index];
            var button = CreateBarberCard(barber);
            _barberButtons[barber.Id] = button;
            Grid.SetRow(button, index / _barberColumnCount);
            Grid.SetColumn(button, index % _barberColumnCount);
            _barberGrid.Children.Add(button);
        }

        UpdateInteractionState();
        UpdateSelectionVisuals();
    }

    private Button CreateBarberCard(Barber barber)
    {
        var button = new Button
        {
            MinHeight = _barberCardMinHeight,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(_barberCardPadding),
            Background = Brush(255, 255, 255),
            BorderBrush = Brush(233, 236, 239),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Tag = barber.Id,
            Content = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Spacing = _barberCardSpacing,
                Children =
                {
                    CreateAvatar(barber, _avatarSize, _avatarFontSize),
                    new TextBlock
                    {
                        Text = barber.DisplayNameWithStation,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        FontSize = _barberNameFontSize,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = Brush(26, 28, 30),
                        TextAlignment = TextAlignment.Center,
                        MaxLines = 2,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        TextWrapping = TextWrapping.WrapWholeWords
                    }
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

    private static Grid CreateAvatar(Barber barber, double size, double fontSize)
    {
        var avatar = new Grid
        {
            Width = size,
            Height = size,
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
            FontSize = fontSize,
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

        return avatar;
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
                Text = "No barbers are selectable right now. Open Barber Public or Local Admin and turn on kiosk availability.",
                FontSize = 16,
                Foreground = Brush(147, 0, 10),
                TextWrapping = TextWrapping.Wrap
            }
        };
    }

    private void UpdateInteractionState()
    {
        var canSelect = CanSelectBarbers;
        var hasBarbers = _barbers.Count > 0;
        _anyBarberButton.IsEnabled = canSelect && hasBarbers;
        _anyBarberButton.Opacity = _anyBarberButton.IsEnabled ? 1 : 0.5;

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
        _printTicketButton.IsEnabled = canSelect && hasSelection && hasBarbers;
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


    private void ResetToAnyBarber()
    {
        _acceptsAnyBarber = true;
        _selectedBarberIds.Clear();
        UpdateSelectionVisuals();
    }

    private void ResetCheckInForm()
    {
        _customerNameInput.Text = string.Empty;
        _customerName = string.Empty;
        _nameErrorText.Visibility = Visibility.Collapsed;
        _barberErrorText.Visibility = Visibility.Collapsed;
        ResetToAnyBarber();
        _checkInPanel.Visibility = Visibility.Visible;
        _ticketPanel.Visibility = Visibility.Collapsed;
        UpdateInteractionState();
    }

    private bool CanSelectBarbers => true;

    private static bool IsSelectable(Barber barber)
    {
        return barber.State is BarberState.NotCheckedIn or BarberState.Available or BarberState.Called or BarberState.InService;
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

