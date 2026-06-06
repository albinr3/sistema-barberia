using Barberia.Core.Domain;
using Barberia.Desktop.Services;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Barberia.Desktop.Views;

public sealed partial class BarberPanelPage : Page
{
    private readonly BarberPanelService _service = new();
    private IReadOnlyList<Barber> _barbers = [];
    private bool _isRefreshingSelector;

    public BarberPanelPage()
    {
        InitializeComponent();
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        LoadPanel();
    }

    private void OnBarberSelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (_isRefreshingSelector)
        {
            return;
        }

        LoadPanel(SelectedBarberId);
    }

    private void OnAvailableClick(object sender, RoutedEventArgs args)
    {
        ExecuteBarberAction(barberId => _service.MarkAvailable(barberId), "Barbero disponible para walk-ins.");
    }

    private void OnOfflineClick(object sender, RoutedEventArgs args)
    {
        ExecuteBarberAction(barberId => _service.MarkOffline(barberId), "Barbero fuera de la cola local.");
    }

    private void OnStartServiceClick(object sender, RoutedEventArgs args)
    {
        StartService();
    }

    private void OnTicketInputKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs args)
    {
        if (args.Key == Windows.System.VirtualKey.Enter)
        {
            StartService();
            args.Handled = true;
        }
    }

    private void StartService()
    {
        if (SelectedBarberId is not { } barberId)
        {
            ShowError("Selecciona un barbero antes de escanear.");
            return;
        }

        try
        {
            var result = _service.StartService(barberId, _ticketInput.Text);
            _ticketInput.Text = string.Empty;
            _messageText.Text = $"{result.TicketNumber} - {result.BarberStationCode} - {result.Message}";
            SetStatus("En servicio", success: true);
            LoadPanel(barberId);
        }
        catch (Exception exception)
        {
            ShowError(exception.Message);
        }
    }

    private void ExecuteBarberAction(Action<Guid> action, string successMessage)
    {
        if (SelectedBarberId is not { } barberId)
        {
            ShowError("Selecciona un barbero.");
            return;
        }

        try
        {
            action(barberId);
            _messageText.Text = successMessage;
            SetStatus("Actualizado", success: true);
            LoadPanel(barberId);
        }
        catch (Exception exception)
        {
            ShowError(exception.Message);
        }
    }

    private void LoadPanel(Guid? selectedBarberId = null)
    {
        try
        {
            var snapshot = _service.Load(selectedBarberId);
            ShowSnapshot(snapshot, selectedBarberId);
            SetStatus("Local", success: true);
        }
        catch (Exception exception)
        {
            _assignedTurns.Children.Clear();
            _assignedTurns.Children.Add(CreateEmptyState(exception.Message));
            ShowError("No se pudo leer la base local.");
        }
    }

    private void ShowSnapshot(BarberPanelSnapshot snapshot, Guid? selectedBarberId)
    {
        var selectedBarber = selectedBarberId is null
            ? null
            : snapshot.Barbers.FirstOrDefault(barber => barber.Id == selectedBarberId.Value);

        _barbers = snapshot.Barbers;
        _isRefreshingSelector = true;
        try
        {
            _barberSelector.ItemsSource = null;
            _barberSelector.ItemsSource = _barbers;

            if (selectedBarber is not null)
            {
                _barberSelector.SelectedItem = _barbers.FirstOrDefault(barber => barber.Id == selectedBarber.Id);
            }
        }
        finally
        {
            _isRefreshingSelector = false;
        }

        _lastRefreshText.Text = $"Actualizado: {snapshot.LoadedAt:hh:mm tt}";

        if (selectedBarber is null)
        {
            _barberStateText.Text = "Sin barbero seleccionado";
            _clientsTodayText.Text = snapshot.Barbers.Count == 0
                ? "No hay barberos registrados en la base local."
                : "Selecciona un barbero para ver sus tickets.";
        }
        else
        {
            _barberStateText.Text = $"{selectedBarber.DisplayNameWithStation} - Estado: {FormatBarberState(selectedBarber.State)}";
            _clientsTodayText.Text = $"Clientes hoy: {selectedBarber.ClientsServedToday}";
        }

        ReplaceAssignedTurns(snapshot.AssignedTurns);
    }

    private void ReplaceAssignedTurns(IReadOnlyList<Turn> turns)
    {
        _assignedTurns.Children.Clear();

        if (turns.Count == 0)
        {
            _assignedTurns.Children.Add(CreateEmptyState("Sin tickets asignados para iniciar."));
            return;
        }

        foreach (var turn in turns)
        {
            _assignedTurns.Children.Add(CreateTurnCard(turn));
        }
    }

    private static UIElement CreateTurnCard(Turn turn)
    {
        return new Border
        {
            Background = turn.Source == TurnSource.Appointment ? Brush(240, 244, 250) : Brush(255, 247, 232),
            BorderBrush = turn.Source == TurnSource.Appointment ? Brush(197, 207, 221) : Brush(242, 181, 88),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16),
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
                                Text = turn.TicketNumber,
                                FontSize = 26,
                                FontWeight = FontWeights.SemiBold,
                                Foreground = Brush(30, 31, 34)
                            },
                            new TextBlock
                            {
                                Text = FormatTurnState(turn.State),
                                FontSize = 13,
                                Foreground = Brush(101, 108, 116)
                            }
                        }
                    },
                    CreateTextBadge(turn.Source == TurnSource.Appointment ? "Cita" : "Walk-in")
                }
            }
        };
    }

    private static UIElement CreateTextBadge(string text)
    {
        var badge = new Border
        {
            Background = Brush(255, 255, 255),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 6, 10, 6),
            VerticalAlignment = VerticalAlignment.Top,
            Child = new TextBlock
            {
                Text = text,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brush(122, 82, 21)
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

    private void ShowError(string message)
    {
        _messageText.Text = message;
        SetStatus("Revisar", success: false);
    }

    private void SetStatus(string text, bool success)
    {
        _statusBadgeText.Text = text;
        _statusBadge.Background = success ? Brush(235, 248, 244) : Brush(255, 240, 238);
        _statusBadge.BorderBrush = success ? Brush(181, 224, 211) : Brush(231, 170, 162);
        _statusBadgeText.Foreground = success ? Brush(17, 105, 88) : Brush(154, 58, 47);
    }

    private Guid? SelectedBarberId => _barberSelector.SelectedItem is Barber barber ? barber.Id : null;

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

    private static string FormatTurnState(TurnState state)
    {
        return state switch
        {
            TurnState.Assigned => "Asignado",
            TurnState.Called => "Llamado",
            _ => state.ToString()
        };
    }

    private static SolidColorBrush Brush(byte red, byte green, byte blue)
    {
        return new SolidColorBrush(ColorHelper.FromArgb(255, red, green, blue));
    }
}
