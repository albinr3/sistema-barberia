using Barberia.Core.Domain;
using Barberia.Desktop.Services;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;

namespace Barberia.Desktop.Views;

public sealed partial class PublicDisplayPage : Page
{
    private const int WaitingGridColumns = 2;
    private const int WaitingGridRows = 6;
    private const int BarberGridColumns = 2;
    private const int NowCallingGridColumns = 2;

    private readonly PublicDisplaySnapshotService _snapshotService = new();
    private readonly DispatcherTimer _refreshTimer = new();

    public event EventHandler? ShellMenuRequested;

    public PublicDisplayPage()
    {
        InitializeComponent();

        AddTemporaryMenuButtonOverlay();
        _displayScrollViewer.SizeChanged += OnDisplayViewportSizeChanged;
        _refreshTimer.Interval = TimeSpan.FromSeconds(30);
        _refreshTimer.Tick += (_, _) => LoadSnapshot();
    }

    private void AddTemporaryMenuButtonOverlay()
    {
        if (Content is not UIElement displayContent)
        {
            return;
        }

        Content = null;

        var menuButton = new Button
        {
            Width = 36,
            Height = 36,
            Margin = new Thickness(0, 16, 16, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Padding = new Thickness(0),
            Background = Brush(255, 255, 255),
            BorderBrush = Brush(197, 197, 216),
            Content = new FontIcon
            {
                FontSize = 18,
                Foreground = Brush(68, 70, 85),
                Glyph = "\uE700"
            }
        };
        ToolTipService.SetToolTip(menuButton, "Show menu");
        menuButton.Click += OnMenuButtonClick;

        Content = new Grid
        {
            Background = Brush(249, 249, 252),
            Children =
            {
                displayContent,
                menuButton
            }
        };
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        LoadSnapshot();
        _refreshTimer.Start();
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        _refreshTimer.Stop();
    }

    private void OnDisplayViewportSizeChanged(object sender, SizeChangedEventArgs args)
    {
        _displayCanvas.MinWidth = Math.Max(0, args.NewSize.Width);
        _displayCanvas.MinHeight = Math.Max(0, args.NewSize.Height);
    }

    private void OnMenuButtonClick(object sender, RoutedEventArgs args)
    {
        ShellMenuRequested?.Invoke(this, EventArgs.Empty);
    }

    private void LoadSnapshot()
    {
        try
        {
            ShowSnapshot(_snapshotService.Load());
        }
        catch (Exception exception)
        {
            _nowCallingSection.Visibility = Visibility.Collapsed;
            _nowCallingItems.Children.Clear();
            _waitingItems.Children.Clear();
            _barberItems.Children.Clear();
            AddSingleGridChild(_waitingItems, CreateEmptyState("Could not read the local database."));
            AddSingleGridChild(_barberItems, CreateEmptyState(exception.Message));
            _waitingTotalText.Text = "0";
            _lastRefreshPill.Text = "No data";
        }
    }

    private void ShowSnapshot(PublicDisplaySnapshot snapshot)
    {
        var called = snapshot.ActiveTurns
            .Where(turn => turn.State is TurnState.Assigned or TurnState.Called)
            .OrderBy(turn => turn.CheckedInAt)
            .Take(4)
            .ToArray();
        var waitingAll = snapshot.ActiveTurns
            .Where(turn => turn.State is TurnState.Waiting)
            .OrderBy(turn => turn.CheckedInAt)
            .ToArray();
        var waiting = waitingAll.Take(WaitingGridColumns * WaitingGridRows).ToArray();
        var activeTurnsByBarber = snapshot.ActiveTurns
            .Where(turn => turn.AssignedBarberId is not null)
            .GroupBy(turn => turn.AssignedBarberId!.Value)
            .ToDictionary(group => group.Key, group => group.OrderBy(turn => GetTurnDisplayPriority(turn.State)).First());
        var orderedBarbers = snapshot.Barbers
            .OrderBy(barber => GetBarberDisplayPriority(barber, snapshot.ProtectedBarberIds.Contains(barber.Id), activeTurnsByBarber))
            .ThenBy(barber => barber.StationNumber ?? int.MaxValue)
            .ThenBy(barber => barber.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        _nowCallingSection.Visibility = called.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        _lastRefreshPill.Text = $"Updated: {snapshot.LoadedAt:hh:mm tt}";
        _waitingTotalText.Text = waitingAll.Length.ToString();

        ReplaceGridChildren(
            _nowCallingItems,
            called.Select(turn => CreateNowCallingCard(turn, snapshot.Barbers)),
            NowCallingGridColumns,
            "No tickets are being called.");
        ReplaceGridChildren(
            _waitingItems,
            waiting.Select(turn => CreateWaitingCard(turn, snapshot.Barbers)),
            WaitingGridColumns,
            "No customers waiting.",
            fillByColumn: true);
        ReplaceGridChildren(
            _barberItems,
            orderedBarbers.Select(barber => CreateBarberCard(
                barber,
                snapshot.ProtectedBarberIds.Contains(barber.Id),
                activeTurnsByBarber.GetValueOrDefault(barber.Id))),
            BarberGridColumns,
            "No active barbers registered.");
    }

    private static FrameworkElement CreateNowCallingCard(Turn turn, IReadOnlyList<Barber> barbers)
    {
        var barber = GetAssignedBarber(turn, barbers);
        var stationCode = barber?.StationCode ?? "B-?";
        var barberName = barber?.DisplayName ?? "Local barber";

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition(),
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 24,
            VerticalAlignment = VerticalAlignment.Center
        };

        grid.Children.Add(new Border
        {
            Width = 8,
            Background = Brush(0, 19, 135),
            CornerRadius = new CornerRadius(8, 0, 0, 8)
        });

        var ticketPanel = new StackPanel
        {
            Margin = new Thickness(24, 0, 0, 0),
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                CreateLabel("Ticket Number"),
                new TextBlock
                {
                    Text = turn.DisplayTicketNumber.ToString(),
                    FontSize = 52,
                    FontWeight = FontWeights.Black,
                    Foreground = Brush(0, 19, 135)
                }
            }
        };
        Grid.SetColumn(ticketPanel, 1);
        grid.Children.Add(ticketPanel);

        var destination = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 22,
            VerticalAlignment = VerticalAlignment.Center
        };
        destination.Children.Add(new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 2,
            Children =
            {
                CreateLabel("Station"),
                new TextBlock
                {
                    Text = stationCode,
                    FontSize = 54,
                    FontWeight = FontWeights.Black,
                    Foreground = Brush(0, 19, 135)
                }
            }
        });

        var avatarPanel = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 5,
            Children =
            {
                CreateAvatar(barber, 64),
                new TextBlock
                {
                    Text = barberName,
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brush(26, 28, 30),
                    HorizontalAlignment = HorizontalAlignment.Center
                }
            }
        };
        Grid.SetColumn(avatarPanel, 1);
        destination.Children.Add(avatarPanel);

        Grid.SetColumn(destination, 2);
        grid.Children.Add(destination);

        var flashOverlay = new Border
        {
            Background = Brush(223, 224, 255),
            Opacity = 0,
            IsHitTestVisible = false
        };

        var cardContent = new Grid
        {
            Children =
            {
                grid,
                flashOverlay
            }
        };

        var card = new Border
        {
            Background = Brush(255, 255, 255),
            BorderBrush = Brush(152, 163, 255),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(0, 16, 18, 16),
            RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5),
            RenderTransform = new ScaleTransform(),
            Child = cardContent
        };

        AttachCallingPopupAnimation(card, flashOverlay);
        return card;
    }

    private static void AttachCallingPopupAnimation(Border card, UIElement flashOverlay)
    {
        Storyboard? storyboard = null;

        card.Loaded += (_, _) =>
        {
            if (card.RenderTransform is not ScaleTransform scale)
            {
                return;
            }

            storyboard?.Stop();
            scale.ScaleX = 1;
            scale.ScaleY = 1;
            flashOverlay.Opacity = 0;

            storyboard = new Storyboard
            {
                RepeatBehavior = RepeatBehavior.Forever
            };

            storyboard.Children.Add(CreateScalePulseAnimation(card, "ScaleX", TimeSpan.Zero));
            storyboard.Children.Add(CreateScalePulseAnimation(card, "ScaleY", TimeSpan.Zero));
            storyboard.Children.Add(CreateFlashAnimation(flashOverlay, TimeSpan.FromMilliseconds(90)));
            storyboard.Begin();
        };

        card.Unloaded += (_, _) =>
        {
            storyboard?.Stop();
            storyboard = null;
        };
    }

    private static Timeline CreateScalePulseAnimation(UIElement target, string property, TimeSpan beginTime)
    {
        var animation = new DoubleAnimation
        {
            From = 1,
            To = 1.035,
            Duration = TimeSpan.FromMilliseconds(575),
            AutoReverse = true,
            BeginTime = beginTime,
            EnableDependentAnimation = true
        };

        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, $"(UIElement.RenderTransform).(ScaleTransform.{property})");
        return animation;
    }

    private static Timeline CreateFlashAnimation(UIElement target, TimeSpan beginTime)
    {
        var animation = new DoubleAnimation
        {
            From = 0,
            To = 0.34,
            Duration = TimeSpan.FromMilliseconds(575),
            AutoReverse = true,
            BeginTime = beginTime,
            EnableDependentAnimation = true
        };

        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, "Opacity");
        return animation;
    }

    private static FrameworkElement CreateWaitingCard(Turn turn, IReadOnlyList<Barber> barbers)
    {
        var customerName = string.IsNullOrWhiteSpace(turn.CustomerName) ? "Walk-in customer" : turn.CustomerName;
        var requestedText = turn.Source == TurnSource.Appointment
            ? "Appointment"
            : $"Requested: {FormatRequestedBarbers(turn, barbers)}";

        var content = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition(),
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 14
        };

        content.Children.Add(new Border
        {
            Width = 80,
            Height = 76,
            Background = Brush(0, 19, 135),
            CornerRadius = new CornerRadius(8),
            Child = new TextBlock
            {
                Text = turn.DisplayTicketNumber.ToString(),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 46,
                FontWeight = FontWeights.Black,
                Foreground = Brush(255, 255, 255)
            }
        });

        var details = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 6,
            Children =
            {
                new TextBlock
                {
                    Text = customerName,
                    FontSize = 20,
                    FontWeight = FontWeights.Black,
                    Foreground = Brush(16, 20, 24),
                    TextTrimming = TextTrimming.CharacterEllipsis
                },
                CreateInlineBadge(requestedText, Brush(232, 232, 234), Brush(26, 28, 30))
            }
        };
        Grid.SetColumn(details, 1);
        content.Children.Add(details);

        var created = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children =
            {
                new TextBlock
                {
                    Text = "Created:",
                    FontSize = 16,
                    FontWeight = FontWeights.Black,
                    Foreground = Brush(0, 19, 135),
                    TextAlignment = TextAlignment.Right
                },
                new TextBlock
                {
                    Text = turn.CheckedInAt.ToString("h:mm tt"),
                    FontSize = 16,
                    FontWeight = FontWeights.Black,
                    Foreground = Brush(0, 19, 135),
                    TextAlignment = TextAlignment.Right
                }
            }
        };
        Grid.SetColumn(created, 2);
        content.Children.Add(created);

        return new Border
        {
            MinHeight = 98,
            Background = Brush(245, 246, 255),
            BorderBrush = Brush(211, 216, 246),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 10, 12, 10),
            Child = content
        };
    }

    private static FrameworkElement CreateBarberCard(Barber barber, bool isProtected, Turn? activeTurn)
    {
        var display = GetBarberDisplay(barber, isProtected, activeTurn);
        var content = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition()
            },
            ColumnSpacing = 14
        };

        content.Children.Add(new Border
        {
            Width = 4,
            Background = display.Stripe,
            CornerRadius = new CornerRadius(8, 0, 0, 8)
        });

        var avatar = CreateAvatar(barber, 64);
        Grid.SetColumn(avatar, 1);
        content.Children.Add(avatar);

        var detailGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(),
                new ColumnDefinition { Width = GridLength.Auto }
            },
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto }
            },
            RowSpacing = 6
        };
        detailGrid.Children.Add(new TextBlock
        {
            Text = barber.DisplayName,
            FontSize = 24,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush(26, 28, 30),
            TextTrimming = TextTrimming.CharacterEllipsis
        });

        var badge = CreateStatusBadge(display.Status, display.BadgeBackground, display.BadgeForeground, display.Dot);
        Grid.SetColumn(badge, 1);
        detailGrid.Children.Add(badge);

        var detail = new TextBlock
        {
            Text = display.Detail,
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush(26, 28, 30)
        };
        Grid.SetRow(detail, 1);
        Grid.SetColumnSpan(detail, 2);
        detailGrid.Children.Add(detail);

        Grid.SetColumn(detailGrid, 2);
        content.Children.Add(detailGrid);

        return new Border
        {
            MinHeight = 98,
            Opacity = barber.State is BarberState.Offline or BarberState.NotCheckedIn ? 0.68 : 1,
            Background = isProtected || barber.State == BarberState.Available ? Brush(247, 248, 255) : Brush(255, 255, 255),
            BorderBrush = isProtected ? Brush(152, 163, 255) : Brush(197, 197, 216),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(0, 16, 16, 16),
            Child = content
        };
    }

    private static BarberDisplay GetBarberDisplay(Barber barber, bool isProtected, Turn? activeTurn)
    {
        if (isProtected)
        {
            return new BarberDisplay(
                "Reserved",
                "Reserved for appointment",
                Brush(242, 181, 88),
                Brush(255, 247, 232),
                Brush(122, 82, 21),
                Brush(242, 181, 88));
        }

        if (activeTurn?.State is TurnState.Assigned or TurnState.Called)
        {
            return new BarberDisplay(
                "Calling",
                $"Calling: {activeTurn.DisplayTicketNumber}",
                Brush(0, 19, 135),
                Brush(223, 224, 255),
                Brush(0, 19, 135),
                Brush(0, 19, 135));
        }

        if (activeTurn?.State == TurnState.InService || barber.State == BarberState.InService)
        {
            var ticketText = activeTurn is null ? "Serving customer" : $"Serving: {activeTurn.DisplayTicketNumber}";
            return new BarberDisplay(
                "Busy",
                ticketText,
                Brush(186, 26, 26),
                Brush(255, 218, 214),
                Brush(147, 0, 10),
                Brush(186, 26, 26));
        }

        if (barber.State == BarberState.Available)
        {
            return new BarberDisplay(
                "Available",
                $"Station {barber.StationCode ?? "B-?"} Ready",
                Brush(34, 197, 94),
                Brush(220, 252, 231),
                Brush(22, 101, 52),
                Brush(34, 197, 94));
        }

        return new BarberDisplay(
            "On Break",
            barber.StationCode is null ? "Not available" : $"{barber.StationCode} Not available",
            Brush(117, 118, 135),
            Brush(232, 232, 234),
            Brush(68, 70, 85),
            Brush(117, 118, 135));
    }

    private static void ReplaceGridChildren(
        Grid grid,
        IEnumerable<FrameworkElement> children,
        int columns,
        string emptyText,
        bool fillByColumn = false)
    {
        grid.Children.Clear();
        grid.RowDefinitions.Clear();
        grid.ColumnDefinitions.Clear();

        var items = children.ToArray();
        if (items.Length == 0)
        {
            AddSingleGridChild(grid, CreateEmptyState(emptyText));
            return;
        }

        for (var column = 0; column < columns; column++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition());
        }

        var rows = fillByColumn
            ? Math.Min(WaitingGridRows, items.Length)
            : (int)Math.Ceiling(items.Length / (double)columns);
        for (var row = 0; row < rows; row++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
            var row = fillByColumn ? index % WaitingGridRows : index / columns;
            var column = fillByColumn ? index / WaitingGridRows : index % columns;
            Grid.SetRow(item, row);
            Grid.SetColumn(item, column);
            grid.Children.Add(item);
        }
    }

    private static void AddSingleGridChild(Grid grid, UIElement child)
    {
        grid.Children.Clear();
        grid.RowDefinitions.Clear();
        grid.ColumnDefinitions.Clear();
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.Children.Add(child);
    }

    private static FrameworkElement CreateAvatar(Barber? barber, double size)
    {
        var imageUri = ProfileImageCatalog.ResolveImageUri(barber?.ProfileImagePath);
        if (imageUri is not null)
        {
            return new Ellipse
            {
                Width = size,
                Height = size,
                Stroke = Brush(197, 197, 216),
                StrokeThickness = 1,
                Fill = new ImageBrush
                {
                    ImageSource = new BitmapImage(imageUri),
                    Stretch = Stretch.UniformToFill
                }
            };
        }

        return new Border
        {
            Width = size,
            Height = size,
            Background = Brush(232, 232, 234),
            BorderBrush = Brush(197, 197, 216),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(size / 2),
            Child = new TextBlock
            {
                Text = GetInitials(barber?.DisplayName),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = size >= 64 ? 22 : 18,
                FontWeight = FontWeights.Black,
                Foreground = Brush(68, 70, 85)
            }
        };
    }

    private static FrameworkElement CreateStatusBadge(string text, Brush background, Brush foreground, Brush dot)
    {
        return new Border
        {
            Background = background,
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(10, 5, 10, 5),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Children =
                {
                    new Ellipse
                    {
                        Width = 8,
                        Height = 8,
                        Fill = dot,
                        VerticalAlignment = VerticalAlignment.Center
                    },
                    new TextBlock
                    {
                        Text = text,
                        FontSize = 14,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = foreground
                    }
                }
            }
        };
    }

    private static UIElement CreateInlineBadge(string text, Brush background, Brush foreground)
    {
        return new Border
        {
            Background = background,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(9, 4, 9, 4),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = new TextBlock
            {
                Text = text,
                FontSize = 18,
                FontWeight = FontWeights.Black,
                Foreground = foreground,
                TextTrimming = TextTrimming.CharacterEllipsis
            }
        };
    }

    private static TextBlock CreateLabel(string text)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush(68, 70, 85),
            CharacterSpacing = 80
        };
    }

    private static UIElement CreateEmptyState(string text)
    {
        return new Border
        {
            Background = Brush(255, 255, 255),
            BorderBrush = Brush(197, 197, 216),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(18),
            Child = new TextBlock
            {
                Text = text,
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brush(68, 70, 85)
            }
        };
    }

    private static string FormatRequestedBarbers(Turn turn, IReadOnlyList<Barber> barbers)
    {
        if (turn.AssignedBarberId is Guid assignedBarberId)
        {
            return barbers.FirstOrDefault(barber => barber.Id == assignedBarberId)?.DisplayName ?? "Local barber";
        }

        if (turn.RequestedBarberIds is null || turn.RequestedBarberIds.Count == 0)
        {
            return "Any Available";
        }

        var requestedNames = turn.RequestedBarberIds
            .Select(requestedId => barbers.FirstOrDefault(barber => barber.Id == requestedId)?.DisplayName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .ToArray();

        return requestedNames.Length == 0 ? "Any Available" : string.Join(", ", requestedNames);
    }

    private static Barber? GetAssignedBarber(Turn turn, IReadOnlyList<Barber> barbers)
    {
        return turn.AssignedBarberId is Guid assignedBarberId
            ? barbers.FirstOrDefault(barber => barber.Id == assignedBarberId)
            : null;
    }

    private static int GetBarberDisplayPriority(
        Barber barber,
        bool isProtected,
        IReadOnlyDictionary<Guid, Turn> activeTurnsByBarber)
    {
        if (barber.State == BarberState.Available && !isProtected)
        {
            return 0;
        }

        if (isProtected)
        {
            return 1;
        }

        if (activeTurnsByBarber.TryGetValue(barber.Id, out var activeTurn))
        {
            return activeTurn.State is TurnState.Assigned or TurnState.Called ? 2 : 3;
        }

        return 4;
    }

    private static int GetTurnDisplayPriority(TurnState state)
    {
        return state switch
        {
            TurnState.Called => 0,
            TurnState.Assigned => 1,
            TurnState.InService => 2,
            _ => 3
        };
    }

    private static string GetInitials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "?";
        }

        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Concat(parts.Take(2).Select(part => char.ToUpperInvariant(part[0])));
    }

    private static SolidColorBrush Brush(byte red, byte green, byte blue)
    {
        return new SolidColorBrush(ColorHelper.FromArgb(255, red, green, blue));
    }

    private sealed record BarberDisplay(
        string Status,
        string Detail,
        Brush Stripe,
        Brush BadgeBackground,
        Brush BadgeForeground,
        Brush Dot);
}
