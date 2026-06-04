using Barberia.Desktop.Services;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace Barberia.Desktop.Views;

public sealed class KioskPage : Page
{
    private readonly KioskCheckInService _checkInService = new();
    private readonly Button _checkInButton = new();
    private readonly Border _statusBadge = new();
    private readonly TextBlock _statusBadgeText = new();
    private readonly TextBlock _ticketNumber = new();
    private readonly TextBlock _assignmentText = new();
    private readonly TextBlock _messageText = new();
    private readonly TextBlock _timeText = new();

    public KioskPage()
    {
        Content = BuildContent();
        ShowReadyState();
    }

    private UIElement BuildContent()
    {
        var root = new ScrollViewer
        {
            Background = Brush(248, 249, 251),
            Content = new StackPanel
            {
                Padding = new Thickness(32, 28, 32, 32),
                Spacing = 18,
                Children =
                {
                    CreateHero(),
                    CreateCheckInPanel(),
                    CreateOperationalNotes()
                }
            }
        };

        return root;
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
            Child = new Grid()
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
                Glyph = "\uE8FA",
                FontSize = 30,
                Foreground = Brush(255, 255, 255)
            }
        };

        var titleStack = new StackPanel
        {
            Margin = new Thickness(18, 2, 0, 0),
            Spacing = 5,
            Children =
            {
                new TextBlock
                {
                    Text = "Check-in de walk-ins",
                    FontSize = 30,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brush(30, 31, 34)
                },
                new TextBlock
                {
                    Text = "Registro local para entrar a la cola de atencion",
                    FontSize = 14,
                    Foreground = Brush(101, 108, 116)
                }
            }
        };

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

    private UIElement CreateCheckInPanel()
    {
        var grid = new Grid
        {
            ColumnSpacing = 14
        };

        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.08, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());

        var actionPanel = new Border
        {
            Background = Brush(255, 255, 255),
            BorderBrush = Brush(226, 230, 235),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(24),
            Child = new StackPanel
            {
                Spacing = 18,
                Children =
                {
                    new TextBlock
                    {
                        Text = "Entrada",
                        FontSize = 22,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = Brush(30, 31, 34)
                    },
                    new TextBlock
                    {
                        Text = "Toca el boton para registrar tu llegada. El sistema asigna el turno usando la cola local.",
                        FontSize = 15,
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = Brush(101, 108, 116)
                    },
                    CreateCheckInButton()
                }
            }
        };

        var resultPanel = new Border
        {
            Background = Brush(255, 255, 255),
            BorderBrush = Brush(226, 230, 235),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(24),
            Child = new StackPanel
            {
                Spacing = 14,
                Children =
                {
                    new TextBlock
                    {
                        Text = "Ticket",
                        FontSize = 22,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = Brush(30, 31, 34)
                    },
                    _ticketNumber,
                    _assignmentText,
                    _messageText,
                    _timeText
                }
            }
        };

        _ticketNumber.FontSize = 34;
        _ticketNumber.FontWeight = FontWeights.SemiBold;
        _ticketNumber.Foreground = Brush(30, 31, 34);

        _assignmentText.FontSize = 18;
        _assignmentText.FontWeight = FontWeights.SemiBold;
        _assignmentText.Foreground = Brush(31, 119, 104);
        _assignmentText.TextWrapping = TextWrapping.Wrap;

        _messageText.FontSize = 14;
        _messageText.Foreground = Brush(101, 108, 116);
        _messageText.TextWrapping = TextWrapping.Wrap;

        _timeText.FontSize = 13;
        _timeText.Foreground = Brush(101, 108, 116);

        Grid.SetColumn(resultPanel, 1);
        grid.Children.Add(actionPanel);
        grid.Children.Add(resultPanel);

        return grid;
    }

    private Button CreateCheckInButton()
    {
        _checkInButton.HorizontalAlignment = HorizontalAlignment.Stretch;
        _checkInButton.MinHeight = 112;
        _checkInButton.Padding = new Thickness(22);
        _checkInButton.Background = Brush(242, 181, 88);
        _checkInButton.BorderBrush = Brush(213, 152, 62);
        _checkInButton.Foreground = Brush(30, 29, 27);
        _checkInButton.Content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 14,
            Children =
            {
                new FontIcon
                {
                    Glyph = "\uE8FB",
                    FontSize = 28
                },
                new TextBlock
                {
                    Text = "Registrar llegada",
                    FontSize = 24,
                    FontWeight = FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center
                }
            }
        };
        _checkInButton.Click += OnCheckInButtonClick;
        ToolTipService.SetToolTip(_checkInButton, "Registrar walk-in local");

        return _checkInButton;
    }

    private UIElement CreateOperationalNotes()
    {
        var grid = new Grid
        {
            ColumnSpacing = 12
        };

        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());

        var cards = new[]
        {
            CreateNoteCard("\uE8A5", "Sin servicio", "El kiosco no pide seleccionar servicio ni precio."),
            CreateNoteCard("\uE753", "Offline", "El registro se guarda primero en SQLite local."),
            CreateNoteCard("\uE8EF", "Asignacion", "La decision se delega al motor de turnos de Core.")
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

    private void OnCheckInButtonClick(object sender, RoutedEventArgs args)
    {
        _checkInButton.IsEnabled = false;
        _statusBadgeText.Text = "Registrando";

        try
        {
            ShowResult(_checkInService.RegisterWalkIn());
        }
        catch (Exception exception)
        {
            ShowError(exception.Message);
        }
        finally
        {
            _checkInButton.IsEnabled = true;
        }
    }

    private void ShowReadyState()
    {
        _statusBadgeText.Text = "Listo";
        _ticketNumber.Text = "Sin ticket";
        _assignmentText.Text = "Esperando registro";
        _messageText.Text = "La proxima llegada generara un ticket local.";
        _timeText.Text = string.Empty;
    }

    private void ShowResult(KioskCheckInResult result)
    {
        _ticketNumber.Text = result.TicketNumber;
        _assignmentText.Text = result.Status == KioskCheckInStatus.Assigned
            ? $"Barbero asignado: {result.AssignedBarberName}"
            : "Turno en espera";
        _messageText.Text = result.Message;
        _timeText.Text = $"Registrado: {result.CheckedInAt:hh:mm tt}";
        _statusBadgeText.Text = result.Status == KioskCheckInStatus.Assigned ? "Asignado" : "En espera";
    }

    private void ShowError(string message)
    {
        _ticketNumber.Text = "No registrado";
        _assignmentText.Text = "Revisar operacion local";
        _messageText.Text = message;
        _timeText.Text = string.Empty;
        _statusBadgeText.Text = "Error";
    }

    private static SolidColorBrush Brush(byte red, byte green, byte blue)
    {
        return new SolidColorBrush(ColorHelper.FromArgb(255, red, green, blue));
    }
}
