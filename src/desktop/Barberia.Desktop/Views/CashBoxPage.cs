using Barberia.Core.Domain;
using Barberia.Desktop.Services;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace Barberia.Desktop.Views;

public sealed class CashBoxPage : Page
{
    private readonly CashBoxCloseService _service = new();
    private readonly ComboBox _barberSelector = new();
    private readonly TextBox _ticketInput = new();
    private readonly TextBox _amountInput = new();
    private readonly TextBlock _messageText = new();
    private readonly TextBlock _lastRefreshText = new();
    private readonly TextBlock _statusBadgeText = new();
    private readonly Border _statusBadge = new();
    private readonly TextBlock _receiptText = new();
    private readonly TextBlock _amountText = new();
    private readonly TextBlock _commissionText = new();
    private IReadOnlyList<Barber> _barbers = [];

    public CashBoxPage()
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
                    CreateOperationalNotes()
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
                Glyph = "\uE8C7",
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
                    Text = "Autocaja",
                    FontSize = 32,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brush(30, 31, 34)
                },
                new TextBlock
                {
                    Text = "Cierre local de servicios pagados en efectivo",
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
        var grid = new Grid { ColumnSpacing = 14 };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());

        var closePanel = CreatePanel("Deposito de efectivo", "El barbero escanea su ticket y registra el monto cobrado", new StackPanel
        {
            Spacing = 14,
            Children =
            {
                _barberSelector,
                CreateTicketInput(),
                CreateAmountInput(),
                CreateCloseButton(),
                _messageText
            }
        });

        var receiptPanel = CreatePanel("Constancia local", "Resumen del ultimo cierre completado", new StackPanel
        {
            Spacing = 12,
            Children =
            {
                CreateMetric("Recibo", _receiptText),
                CreateMetric("Monto", _amountText),
                CreateMetric("Comision", _commissionText)
            }
        });

        _barberSelector.HorizontalAlignment = HorizontalAlignment.Stretch;
        _barberSelector.MinHeight = 46;
        _barberSelector.PlaceholderText = "Seleccionar barbero";
        _barberSelector.DisplayMemberPath = nameof(Barber.DisplayName);

        _messageText.FontSize = 14;
        _messageText.TextWrapping = TextWrapping.Wrap;
        _messageText.Foreground = Brush(101, 108, 116);

        Grid.SetColumn(receiptPanel, 1);
        grid.Children.Add(closePanel);
        grid.Children.Add(receiptPanel);

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
                                Foreground = Brush(101, 108, 116),
                                TextWrapping = TextWrapping.Wrap
                            }
                        }
                    },
                    content
                }
            }
        };
    }

    private UIElement CreateTicketInput()
    {
        _ticketInput.Header = "Ticket";
        _ticketInput.PlaceholderText = "Escanear o escribir numero de ticket";
        _ticketInput.MinHeight = 48;
        _ticketInput.FontSize = 17;
        _ticketInput.KeyDown += OnInputKeyDown;
        return _ticketInput;
    }

    private UIElement CreateAmountInput()
    {
        _amountInput.Header = "Monto cobrado en efectivo";
        _amountInput.PlaceholderText = "0.00";
        _amountInput.MinHeight = 48;
        _amountInput.FontSize = 17;
        _amountInput.InputScope = new Microsoft.UI.Xaml.Input.InputScope
        {
            Names =
            {
                new Microsoft.UI.Xaml.Input.InputScopeName(Microsoft.UI.Xaml.Input.InputScopeNameValue.Number)
            }
        };
        _amountInput.KeyDown += OnInputKeyDown;
        return _amountInput;
    }

    private Button CreateCloseButton()
    {
        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinHeight = 56,
            Padding = new Thickness(16, 10, 16, 10),
            Background = Brush(242, 181, 88),
            BorderBrush = Brush(213, 152, 62),
            Foreground = Brush(30, 29, 27),
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = 10,
                Children =
                {
                    new FontIcon { Glyph = "\uE73E", FontSize = 18 },
                    new TextBlock
                    {
                        Text = "Cerrar servicio",
                        FontSize = 16,
                        FontWeight = FontWeights.SemiBold,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                }
            }
        };

        ToolTipService.SetToolTip(button, "Cerrar servicio en efectivo");
        button.Click += OnCloseClick;
        return button;
    }

    private static UIElement CreateMetric(string label, TextBlock value)
    {
        value.FontSize = 20;
        value.FontWeight = FontWeights.SemiBold;
        value.Foreground = Brush(30, 31, 34);
        value.TextWrapping = TextWrapping.Wrap;

        return new Border
        {
            Background = Brush(248, 249, 251),
            BorderBrush = Brush(226, 230, 235),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16),
            Child = new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    new TextBlock
                    {
                        Text = label,
                        FontSize = 12,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = Brush(101, 108, 116)
                    },
                    value
                }
            }
        };
    }

    private UIElement CreateOperationalNotes()
    {
        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());

        var cards = new[]
        {
            CreateNoteCard("\uE8C7", "Solo efectivo", "No se registran tarjetas ni pagos online en Fase 1."),
            CreateNoteCard("\uE8D7", "Hardware", "La impresion y el cash drawer pasan por abstracciones simuladas."),
            CreateNoteCard("\uE8EF", "Cola", "Al cerrar, el barbero vuelve disponible y pasa al final de la rotacion.")
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
        LoadCashBox();
        ShowReadyState();
    }

    private void OnCloseClick(object sender, RoutedEventArgs args)
    {
        CloseService();
    }

    private void OnInputKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs args)
    {
        if (args.Key == Windows.System.VirtualKey.Enter)
        {
            CloseService();
            args.Handled = true;
        }
    }

    private void LoadCashBox()
    {
        try
        {
            var snapshot = _service.Load();
            _barbers = snapshot.Barbers;
            _barberSelector.ItemsSource = _barbers;
            _lastRefreshText.Text = $"Actualizado: {snapshot.LoadedAt:hh:mm tt}";
            SetStatus("Local", success: true);
        }
        catch (Exception exception)
        {
            ShowError(exception.Message);
        }
    }

    private void CloseService()
    {
        if (_barberSelector.SelectedItem is not Barber barber)
        {
            ShowError("Selecciona un barbero.");
            return;
        }

        try
        {
            var result = _service.CloseService(barber.Id, _ticketInput.Text, _amountInput.Text);
            _receiptText.Text = result.ReceiptNumber;
            _amountText.Text = $"{result.Amount:0.00}";
            _commissionText.Text = $"{result.Commission:0.00}";
            _messageText.Text = $"{result.TicketNumber} - {result.Message}";
            _ticketInput.Text = string.Empty;
            _amountInput.Text = string.Empty;
            SetStatus("Cerrado", success: true);
            LoadCashBox();
            _barberSelector.SelectedItem = _barbers.FirstOrDefault(candidate => candidate.Id == barber.Id);
        }
        catch (Exception exception)
        {
            ShowError(exception.Message);
        }
    }

    private void ShowReadyState()
    {
        _receiptText.Text = "Sin recibo";
        _amountText.Text = "0.00";
        _commissionText.Text = "0.00";
        _messageText.Text = "Esperando ticket, barbero y monto cobrado.";
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

    private static SolidColorBrush Brush(byte red, byte green, byte blue)
    {
        return new SolidColorBrush(ColorHelper.FromArgb(255, red, green, blue));
    }
}
