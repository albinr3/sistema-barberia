using Barberia.Core.Domain;
using Barberia.Desktop.Services;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace Barberia.Desktop.Views;

public sealed class BarberPanelPage : Page
{
    private readonly BarberPanelService _service = new();
    private readonly ComboBox _barberSelector = new();
    private readonly TextBox _ticketInput = new();
    private readonly StackPanel _assignedTurns = new() { Spacing = 10 };
    private readonly TextBlock _barberStateText = new();
    private readonly TextBlock _clientsTodayText = new();
    private readonly TextBlock _lastRefreshText = new();
    private readonly TextBlock _messageText = new();
    private readonly TextBlock _statusBadgeText = new();
    private readonly Border _statusBadge = new();
    private IReadOnlyList<Barber> _barbers = [];
    private bool _isRefreshingSelector;

    public BarberPanelPage()
    {
        Content = BuildContent();
        Loaded += OnLoaded;
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
                    CreateMainGrid(),
                    CreateFlowNotes()
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
                Glyph = "\uE77B",
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
                    Text = "Panel de barbero",
                    FontSize = 32,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brush(30, 31, 34)
                },
                new TextBlock
                {
                    Text = "Disponibilidad local, tickets asignados e inicio de atencion",
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

        Grid.SetColumn(titleStack, 1);
        Grid.SetColumn(_statusBadge, 2);
        layout.Children.Add(iconBox);
        layout.Children.Add(titleStack);
        layout.Children.Add(_statusBadge);

        return hero;
    }

    private UIElement CreateMainGrid()
    {
        var grid = new Grid
        {
            ColumnSpacing = 14
        };

        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.9, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.1, GridUnitType.Star) });

        var statePanel = CreatePanel("Estado operativo", "Seleccion local del barbero", new StackPanel
        {
            Spacing = 14,
            Children =
            {
                _barberSelector,
                CreateStateSummary(),
                CreateAvailabilityActions()
            }
        });

        var ticketPanel = CreatePanel("Escaneo de ticket", "Inicia atencion solo con ticket asignado", new StackPanel
        {
            Spacing = 14,
            Children =
            {
                CreateTicketInput(),
                CreateStartButton(),
                _messageText,
                CreateAssignedTurnsPanel()
            }
        });

        _barberSelector.HorizontalAlignment = HorizontalAlignment.Stretch;
        _barberSelector.MinHeight = 44;
        _barberSelector.PlaceholderText = "Seleccionar barbero";
        _barberSelector.DisplayMemberPath = nameof(Barber.DisplayName);
        _barberSelector.SelectionChanged += OnBarberSelectionChanged;

        _messageText.FontSize = 14;
        _messageText.TextWrapping = TextWrapping.Wrap;
        _messageText.Foreground = Brush(101, 108, 116);

        Grid.SetColumn(ticketPanel, 1);
        grid.Children.Add(statePanel);
        grid.Children.Add(ticketPanel);

        return grid;
    }

    private static Border CreatePanel(string title, string subtitle, UIElement content)
    {
        return new Border
        {
            Background = Brush(255, 255, 255),
            BorderBrush = Brush(226, 230, 235),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(22),
            Child = new StackPanel
            {
                Spacing = 16,
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

    private UIElement CreateStateSummary()
    {
        return new Border
        {
            Background = Brush(248, 249, 251),
            BorderBrush = Brush(226, 230, 235),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16),
            Child = new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    _barberStateText,
                    _clientsTodayText
                }
            }
        };
    }

    private UIElement CreateAvailabilityActions()
    {
        var grid = new Grid { ColumnSpacing = 10 };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());

        var availableButton = CreateActionButton("\uE8FB", "Disponible", Brush(31, 119, 104), OnAvailableClick);
        var offlineButton = CreateActionButton("\uE711", "Fuera de linea", Brush(63, 78, 97), OnOfflineClick);

        Grid.SetColumn(offlineButton, 1);
        grid.Children.Add(availableButton);
        grid.Children.Add(offlineButton);

        return grid;
    }

    private UIElement CreateTicketInput()
    {
        _ticketInput.Header = "Ticket";
        _ticketInput.PlaceholderText = "Escanear o escribir numero de ticket";
        _ticketInput.MinHeight = 48;
        _ticketInput.FontSize = 17;
        _ticketInput.KeyDown += OnTicketInputKeyDown;

        return _ticketInput;
    }

    private Button CreateStartButton()
    {
        return CreateActionButton("\uE768", "Iniciar atencion", Brush(242, 181, 88), OnStartServiceClick);
    }

    private static Button CreateActionButton(string glyph, string text, Brush background, RoutedEventHandler click)
    {
        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinHeight = 54,
            Padding = new Thickness(16, 10, 16, 10),
            Background = background,
            BorderBrush = background,
            Foreground = Brush(30, 29, 27),
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = 10,
                Children =
                {
                    new FontIcon { Glyph = glyph, FontSize = 18 },
                    new TextBlock
                    {
                        Text = text,
                        FontSize = 16,
                        FontWeight = FontWeights.SemiBold,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                }
            }
        };

        ToolTipService.SetToolTip(button, text);
        button.Click += click;
        return button;
    }

    private UIElement CreateAssignedTurnsPanel()
    {
        return new StackPanel
        {
            Spacing = 10,
            Children =
            {
                new TextBlock
                {
                    Text = "Tickets asignados",
                    FontSize = 18,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brush(30, 31, 34)
                },
                _assignedTurns
            }
        };
    }

    private UIElement CreateFlowNotes()
    {
        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());

        var cards = new[]
        {
            CreateNoteCard("\uE73E", "Local", "El panel lee y escribe en SQLite sin depender de internet."),
            CreateNoteCard("\uE8A5", "Sin cierre", "No hay boton de terminar servicio en este panel."),
            CreateNoteCard("\uE8EF", "Autocaja", "El pago y cierre operativo ocurren en autocaja.")
        };

        for (var index = 0; index < cards.Length; index++)
        {
            Grid.SetColumn(cards[index], index);
            grid.Children.Add(cards[index]);
        }

        return grid;
    }

    private static Border CreateNoteCard(string glyph, string title, string detail)
    {
        return new Border
        {
            Background = Brush(255, 255, 255),
            BorderBrush = Brush(226, 230, 235),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(18),
            Child = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    new Border
                    {
                        Width = 38,
                        Height = 38,
                        CornerRadius = new CornerRadius(8),
                        Background = Brush(235, 248, 244),
                        Child = new FontIcon
                        {
                            Glyph = glyph,
                            FontSize = 18,
                            Foreground = Brush(31, 119, 104)
                        }
                    },
                    new TextBlock
                    {
                        Text = title,
                        FontSize = 17,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = Brush(30, 31, 34)
                    },
                    new TextBlock
                    {
                        Text = detail,
                        FontSize = 13,
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = Brush(101, 108, 116)
                    }
                }
            }
        };
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
            _messageText.Text = $"{result.TicketNumber} - {result.Message}";
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
            _barberStateText.Text = $"Estado: {FormatBarberState(selectedBarber.State)}";
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
