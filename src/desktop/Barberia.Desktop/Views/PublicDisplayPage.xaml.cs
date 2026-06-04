using Barberia.Core.Domain;
using Barberia.Desktop.Services;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Barberia.Desktop.Views;

public sealed partial class PublicDisplayPage : Page
{
    private readonly PublicDisplaySnapshotService _snapshotService = new();
    private readonly DispatcherTimer _refreshTimer = new();

    public PublicDisplayPage()
    {
        InitializeComponent();

        _refreshTimer.Interval = TimeSpan.FromSeconds(30);
        _refreshTimer.Tick += (_, _) => LoadSnapshot();
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
