using Barberia.Core.Domain;
using Barberia.Desktop.Services;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace Barberia.Desktop.Views;

public sealed class PublicDisplayPage : Page
{
    private readonly PublicDisplaySnapshotService _snapshotService = new();
    private readonly DispatcherTimer _refreshTimer = new();
    private readonly StackPanel _calledTurns = new() { Spacing = 12 };
    private readonly StackPanel _waitingTurns = new() { Spacing = 10 };
    private readonly StackPanel _barberStates = new() { Spacing = 10 };
    private readonly StackPanel _appointments = new() { Spacing = 10 };
    private readonly TextBlock _lastRefreshText = new();
    private readonly TextBlock _calledCountText = new();
    private readonly TextBlock _waitingCountText = new();
    private readonly TextBlock _appointmentCountText = new();
    private readonly Border _statusBadge = new();
    private readonly TextBlock _statusBadgeText = new();

    public PublicDisplayPage()
    {
        Content = BuildContent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;

        _refreshTimer.Interval = TimeSpan.FromSeconds(30);
        _refreshTimer.Tick += (_, _) => LoadSnapshot();
    }

    private UIElement BuildContent()
    {
        return new ScrollViewer
        {
            Background = Brush(248, 249, 251),
            Content = new StackPanel
            {
                Padding = new Thickness(32, 28, 32, 32),
                Spacing = 18,
                Children =
                {
                    CreateHero(),
                    CreateSummaryRow(),
                    CreateDisplayGrid()
                }
            }
        };
    }

    private UIElement CreateHero()
    {
        var hero = new Border
        {
            Background = Brush(255, 255, 255),
            BorderBrush = Brush(226, 230, 235),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(24),
            Child = new Grid { ColumnSpacing = 18 }
        };

        var layout = (Grid)hero.Child;
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        layout.ColumnDefinitions.Add(new ColumnDefinition());
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var iconBox = new Border
        {
            Width = 64,
            Height = 64,
            CornerRadius = new CornerRadius(8),
            Background = Brush(31, 119, 104),
            Child = new FontIcon
            {
                Glyph = "\uE8A7",
                FontSize = 30,
                Foreground = Brush(255, 255, 255)
            }
        };

        var titleStack = new StackPanel
        {
            Spacing = 5,
            Children =
            {
                new TextBlock
                {
                    Text = "Sala de espera",
                    FontSize = 32,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brush(30, 31, 34)
                },
                new TextBlock
                {
                    Text = "Turnos, llamados y citas desde la base local",
                    FontSize = 15,
                    Foreground = Brush(101, 108, 116)
                },
                _lastRefreshText
            }
        };

        _lastRefreshText.FontSize = 13;
        _lastRefreshText.Foreground = Brush(101, 108, 116);

        _statusBadge.Background = Brush(235, 248, 244);
        _statusBadge.BorderBrush = Brush(181, 224, 211);
        _statusBadge.BorderThickness = new Thickness(1);
        _statusBadge.CornerRadius = new CornerRadius(8);
        _statusBadge.Padding = new Thickness(12, 8, 12, 8);
        _statusBadge.Child = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 7,
            Children =
            {
                new Ellipse
                {
                    Width = 8,
                    Height = 8,
                    Fill = Brush(31, 119, 104),
                    VerticalAlignment = VerticalAlignment.Center
                },
                _statusBadgeText
            }
        };

        _statusBadgeText.FontSize = 13;
        _statusBadgeText.Foreground = Brush(17, 105, 88);
        _statusBadgeText.Text = "Local";

        Grid.SetColumn(titleStack, 1);
        Grid.SetColumn(_statusBadge, 2);
        layout.Children.Add(iconBox);
        layout.Children.Add(titleStack);
        layout.Children.Add(_statusBadge);

        return hero;
    }

    private UIElement CreateSummaryRow()
    {
        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());

        var cards = new[]
        {
            CreateSummaryCard("\uE789", "Llamados", _calledCountText, Brush(255, 247, 232), Brush(122, 82, 21)),
            CreateSummaryCard("\uE81C", "En sala", _waitingCountText, Brush(235, 248, 244), Brush(17, 105, 88)),
            CreateSummaryCard("\uE787", "Citas proximas", _appointmentCountText, Brush(240, 244, 250), Brush(63, 78, 97))
        };

        for (var index = 0; index < cards.Length; index++)
        {
            Grid.SetColumn(cards[index], index);
            grid.Children.Add(cards[index]);
        }

        return grid;
    }

    private static Border CreateSummaryCard(
        string glyph,
        string title,
        TextBlock valueText,
        Brush badgeBrush,
        Brush accentBrush)
    {
        valueText.FontSize = 34;
        valueText.FontWeight = FontWeights.SemiBold;
        valueText.Foreground = Brush(30, 31, 34);

        return new Border
        {
            Background = Brush(255, 255, 255),
            BorderBrush = Brush(226, 230, 235),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(20),
            Child = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(),
                    new ColumnDefinition { Width = GridLength.Auto }
                },
                Children =
                {
                    new StackPanel
                    {
                        Spacing = 3,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = title,
                                FontSize = 14,
                                Foreground = Brush(101, 108, 116)
                            },
                            valueText
                        }
                    },
                    CreateIconBadge(glyph, badgeBrush, accentBrush)
                }
            }
        };
    }

    private UIElement CreateDisplayGrid()
    {
        var grid = new Grid
        {
            ColumnSpacing = 14,
            RowSpacing = 14
        };

        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.25, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.RowDefinitions.Add(new RowDefinition());
        grid.RowDefinitions.Add(new RowDefinition());

        var calledPanel = CreatePanel("Llamados ahora", "Turnos listos para pasar", _calledTurns);
        var waitingPanel = CreatePanel("Proximos en cola", "Walk-ins y citas ya registradas", _waitingTurns);
        var barberPanel = CreatePanel("Barberos", "Disponibilidad visible de la sala", _barberStates);
        var appointmentPanel = CreatePanel("Citas programadas", "Reservas sincronizadas hacia local", _appointments);

        Grid.SetRowSpan(calledPanel, 2);
        Grid.SetColumn(waitingPanel, 1);
        Grid.SetColumn(barberPanel, 1);
        Grid.SetRow(barberPanel, 1);
        Grid.SetColumn(appointmentPanel, 0);
        Grid.SetRow(appointmentPanel, 1);

        grid.Children.Add(calledPanel);
        grid.Children.Add(waitingPanel);
        grid.Children.Add(barberPanel);

        return new Grid
        {
            RowSpacing = 14,
            RowDefinitions =
            {
                new RowDefinition(),
                new RowDefinition { Height = GridLength.Auto }
            },
            Children =
            {
                grid,
                appointmentPanel
            }
        };
    }

    private static Border CreatePanel(string title, string subtitle, StackPanel content)
    {
        return new Border
        {
            Background = Brush(255, 255, 255),
            BorderBrush = Brush(226, 230, 235),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(20),
            Child = new StackPanel
            {
                Spacing = 14,
                Children =
                {
                    new StackPanel
                    {
                        Spacing = 2,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = title,
                                FontSize = 22,
                                FontWeight = FontWeights.SemiBold,
                                Foreground = Brush(30, 31, 34)
                            },
                            new TextBlock
                            {
                                Text = subtitle,
                                FontSize = 13,
                                Foreground = Brush(101, 108, 116)
                            }
                        }
                    },
                    content
                }
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

    private void LoadSnapshot()
    {
        try
        {
            ShowSnapshot(_snapshotService.Load());
            _statusBadgeText.Text = "Local";
            _statusBadge.Background = Brush(235, 248, 244);
            _statusBadge.BorderBrush = Brush(181, 224, 211);
        }
        catch (Exception exception)
        {
            _calledTurns.Children.Clear();
            _waitingTurns.Children.Clear();
            _barberStates.Children.Clear();
            _appointments.Children.Clear();
            _calledTurns.Children.Add(CreateEmptyState("No se pudo leer la base local."));
            _waitingTurns.Children.Add(CreateEmptyState(exception.Message));
            _calledCountText.Text = "0";
            _waitingCountText.Text = "0";
            _appointmentCountText.Text = "0";
            _lastRefreshText.Text = "Sin datos actualizados";
            _statusBadgeText.Text = "Error local";
            _statusBadge.Background = Brush(255, 240, 238);
            _statusBadge.BorderBrush = Brush(231, 170, 162);
        }
    }

    private void ShowSnapshot(PublicDisplaySnapshot snapshot)
    {
        var called = snapshot.ActiveTurns
            .Where(turn => turn.State is TurnState.Assigned or TurnState.Called)
            .Take(5)
            .ToArray();
        var waiting = snapshot.ActiveTurns
            .Where(turn => turn.State is TurnState.Waiting or TurnState.InService)
            .Take(8)
            .ToArray();
        var upcomingAppointments = snapshot.Appointments
            .Where(appointment => appointment.State is AppointmentState.Confirmed or AppointmentState.ProtectionStarted or AppointmentState.CheckedIn)
            .Take(6)
            .ToArray();

        _calledCountText.Text = called.Length.ToString();
        _waitingCountText.Text = waiting.Length.ToString();
        _appointmentCountText.Text = upcomingAppointments.Length.ToString();
        _lastRefreshText.Text = $"Actualizado: {snapshot.LoadedAt:hh:mm tt}";

        ReplaceChildren(_calledTurns, called.Select(turn => CreateTurnCard(turn, snapshot.Barbers, large: true)), "Sin turnos llamados.");
        ReplaceChildren(_waitingTurns, waiting.Select(turn => CreateTurnCard(turn, snapshot.Barbers, large: false)), "Sin turnos en espera.");
        ReplaceChildren(_barberStates, snapshot.Barbers.Select(barber => CreateBarberCard(barber, snapshot.ProtectedBarberIds.Contains(barber.Id))), "Sin barberos registrados.");
        ReplaceChildren(_appointments, upcomingAppointments.Select(appointment => CreateAppointmentCard(appointment, snapshot.Barbers, snapshot.ProtectedBarberIds.Contains(appointment.BarberId))), "Sin citas sincronizadas proximas.");
    }

    private static void ReplaceChildren(StackPanel panel, IEnumerable<UIElement> children, string emptyText)
    {
        panel.Children.Clear();
        var added = false;

        foreach (var child in children)
        {
            panel.Children.Add(child);
            added = true;
        }

        if (!added)
        {
            panel.Children.Add(CreateEmptyState(emptyText));
        }
    }

    private static UIElement CreateTurnCard(Turn turn, IReadOnlyList<Barber> barbers, bool large)
    {
        var barberName = turn.AssignedBarberId is null
            ? "Sin asignar"
            : barbers.FirstOrDefault(barber => barber.Id == turn.AssignedBarberId)?.DisplayName ?? "Barbero local";

        return new Border
        {
            Background = turn.Source == TurnSource.Appointment ? Brush(240, 244, 250) : Brush(255, 247, 232),
            BorderBrush = turn.Source == TurnSource.Appointment ? Brush(197, 207, 221) : Brush(242, 181, 88),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(18),
            Child = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(),
                    new ColumnDefinition { Width = GridLength.Auto }
                },
                Children =
                {
                    new StackPanel
                    {
                        Spacing = 4,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = turn.TicketNumber,
                                FontSize = large ? 46 : 25,
                                FontWeight = FontWeights.SemiBold,
                                Foreground = Brush(30, 31, 34)
                            },
                            new TextBlock
                            {
                                Text = barberName,
                                FontSize = large ? 20 : 15,
                                FontWeight = FontWeights.SemiBold,
                                Foreground = Brush(31, 119, 104)
                            }
                        }
                    },
                    CreateSourceBadge(turn.Source)
                }
            }
        };
    }

    private static UIElement CreateBarberCard(Barber barber, bool isProtected)
    {
        var statusText = isProtected ? "Reservado para cita" : FormatBarberState(barber.State);
        var badgeBackground = isProtected ? Brush(255, 247, 232) : GetBarberBadgeBackground(barber.State);
        var badgeForeground = isProtected ? Brush(122, 82, 21) : GetBarberBadgeForeground(barber.State);

        return new Border
        {
            Background = Brush(255, 255, 255),
            BorderBrush = isProtected ? Brush(242, 181, 88) : Brush(226, 230, 235),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14),
            Child = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(),
                    new ColumnDefinition { Width = GridLength.Auto }
                },
                Children =
                {
                    new StackPanel
                    {
                        Spacing = 3,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = barber.DisplayName,
                                FontSize = 18,
                                FontWeight = FontWeights.SemiBold,
                                Foreground = Brush(30, 31, 34)
                            },
                            new TextBlock
                            {
                                Text = $"Clientes hoy: {barber.ClientsServedToday}",
                                FontSize = 12,
                                Foreground = Brush(101, 108, 116)
                            }
                        }
                    },
                    CreateTextBadge(statusText, badgeBackground, badgeForeground)
                }
            }
        };
    }

    private static UIElement CreateAppointmentCard(
        AppointmentReservation appointment,
        IReadOnlyList<Barber> barbers,
        bool isProtected)
    {
        var barberName = barbers.FirstOrDefault(barber => barber.Id == appointment.BarberId)?.DisplayName
            ?? "Barbero local";

        return new Border
        {
            Background = Brush(255, 255, 255),
            BorderBrush = isProtected ? Brush(242, 181, 88) : Brush(226, 230, 235),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14),
            Child = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(),
                    new ColumnDefinition { Width = GridLength.Auto }
                },
                Children =
                {
                    new StackPanel
                    {
                        Spacing = 3,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = $"{appointment.ScheduledFor:hh:mm tt} - {barberName}",
                                FontSize = 18,
                                FontWeight = FontWeights.SemiBold,
                                Foreground = Brush(30, 31, 34)
                            },
                            new TextBlock
                            {
                                Text = isProtected ? "Ventana de proteccion activa" : FormatAppointmentState(appointment.State),
                                FontSize = 13,
                                Foreground = Brush(101, 108, 116)
                            }
                        }
                    },
                    CreateTextBadge("Cita", Brush(240, 244, 250), Brush(63, 78, 97))
                }
            }
        };
    }

    private static UIElement CreateSourceBadge(TurnSource source)
    {
        return CreateTextBadge(
            source == TurnSource.Appointment ? "Cita" : "Walk-in",
            source == TurnSource.Appointment ? Brush(240, 244, 250) : Brush(255, 255, 255),
            source == TurnSource.Appointment ? Brush(63, 78, 97) : Brush(122, 82, 21));
    }

    private static UIElement CreateTextBadge(string text, Brush background, Brush foreground)
    {
        return new Border
        {
            Background = background,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 6, 10, 6),
            VerticalAlignment = VerticalAlignment.Top,
            Child = new TextBlock
            {
                Text = text,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = foreground
            }
        };
    }

    private static UIElement CreateIconBadge(string glyph, Brush background, Brush foreground)
    {
        var badge = new Border
        {
            Width = 42,
            Height = 42,
            CornerRadius = new CornerRadius(8),
            Background = background,
            Child = new FontIcon
            {
                Glyph = glyph,
                FontSize = 19,
                Foreground = foreground
            }
        };

        Grid.SetColumn(badge, 1);
        return badge;
    }

    private static UIElement CreateEmptyState(string text)
    {
        return new Border
        {
            Background = Brush(248, 249, 251),
            BorderBrush = Brush(226, 230, 235),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16),
            Child = new TextBlock
            {
                Text = text,
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brush(101, 108, 116)
            }
        };
    }

    private static string FormatBarberState(BarberState state)
    {
        return state switch
        {
            BarberState.NotCheckedIn => "No ha llegado",
            BarberState.Available => "Disponible",
            BarberState.Called => "Llamando",
            BarberState.InService => "En servicio",
            BarberState.Offline => "Fuera de linea",
            _ => state.ToString()
        };
    }

    private static string FormatAppointmentState(AppointmentState state)
    {
        return state switch
        {
            AppointmentState.Confirmed => "Confirmada",
            AppointmentState.ProtectionStarted => "Protegida",
            AppointmentState.CheckedIn => "Cliente llego",
            AppointmentState.NoShow => "No show",
            AppointmentState.Rescheduled => "Reprogramada",
            AppointmentState.Cancelled => "Cancelada",
            _ => state.ToString()
        };
    }

    private static Brush GetBarberBadgeBackground(BarberState state)
    {
        return state switch
        {
            BarberState.Available => Brush(235, 248, 244),
            BarberState.Called or BarberState.InService => Brush(240, 244, 250),
            _ => Brush(248, 249, 251)
        };
    }

    private static Brush GetBarberBadgeForeground(BarberState state)
    {
        return state switch
        {
            BarberState.Available => Brush(17, 105, 88),
            BarberState.Called or BarberState.InService => Brush(63, 78, 97),
            _ => Brush(101, 108, 116)
        };
    }

    private static SolidColorBrush Brush(byte red, byte green, byte blue)
    {
        return new SolidColorBrush(ColorHelper.FromArgb(255, red, green, blue));
    }
}
