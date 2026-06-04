using Barberia.Data.Models;
using Barberia.Data.Reports;
using Barberia.Desktop.Services;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace Barberia.Desktop.Views;

public sealed class ReportsPage : Page
{
    private readonly AdminReportsService _service = new();
    private readonly DatePicker _reportDatePicker = new();
    private readonly TextBlock _lastRefreshText = new();
    private readonly TextBlock _statusBadgeText = new();
    private readonly Border _statusBadge = new();
    private readonly TextBlock _checkInsText = new();
    private readonly TextBlock _completedText = new();
    private readonly TextBlock _cashText = new();
    private readonly TextBlock _commissionText = new();
    private readonly TextBlock _walkInsText = new();
    private readonly TextBlock _appointmentsText = new();
    private readonly TextBlock _activeTurnsText = new();
    private readonly TextBlock _issuesText = new();
    private readonly TextBlock _messageText = new();
    private readonly StackPanel _barberRows = new() { Spacing = 10 };
    private readonly StackPanel _paymentRows = new() { Spacing = 10 };

    public ReportsPage()
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
                    CreateSummaryRow(),
                    CreateReportGrid(),
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
                Glyph = "\uE9D2",
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
                    Text = "Reportes locales",
                    FontSize = 32,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brush(30, 31, 34)
                },
                new TextBlock
                {
                    Text = "Operacion, efectivo y comisiones desde SQLite",
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

        var actions = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                _statusBadge,
                CreateDateActions()
            }
        };

        Grid.SetColumn(titleStack, 1);
        Grid.SetColumn(actions, 2);
        layout.Children.Add(iconBox);
        layout.Children.Add(titleStack);
        layout.Children.Add(actions);

        return hero;
    }

    private UIElement CreateDateActions()
    {
        _reportDatePicker.Date = DateTimeOffset.Now;
        _reportDatePicker.MinWidth = 210;

        var refreshButton = new Button
        {
            MinHeight = 42,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(14, 8, 14, 8),
            Background = Brush(242, 181, 88),
            BorderBrush = Brush(213, 152, 62),
            Foreground = Brush(30, 29, 27),
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = 8,
                Children =
                {
                    new FontIcon { Glyph = "\uE72C", FontSize = 16 },
                    new TextBlock
                    {
                        Text = "Actualizar",
                        FontSize = 14,
                        FontWeight = FontWeights.SemiBold,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                }
            }
        };

        refreshButton.Click += OnRefreshClick;
        ToolTipService.SetToolTip(refreshButton, "Actualizar reporte local");

        return new StackPanel
        {
            Spacing = 8,
            Children =
            {
                _reportDatePicker,
                refreshButton
            }
        };
    }

    private UIElement CreateSummaryRow()
    {
        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());

        var cards = new[]
        {
            CreateSummaryCard("\uE8A5", "Check-ins", _checkInsText, Brush(235, 248, 244), Brush(17, 105, 88)),
            CreateSummaryCard("\uE73E", "Cierres", _completedText, Brush(240, 244, 250), Brush(63, 78, 97)),
            CreateSummaryCard("\uE8C7", "Efectivo", _cashText, Brush(255, 247, 232), Brush(122, 82, 21)),
            CreateSummaryCard("\uE9D2", "Comision", _commissionText, Brush(248, 249, 251), Brush(101, 108, 116))
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
        valueText.FontSize = 30;
        valueText.FontWeight = FontWeights.SemiBold;
        valueText.Foreground = Brush(30, 31, 34);
        valueText.TextWrapping = TextWrapping.Wrap;

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

    private UIElement CreateReportGrid()
    {
        var grid = new Grid
        {
            ColumnSpacing = 14,
            RowSpacing = 14
        };

        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.15, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());

        var barberPanel = CreatePanel("Barberos", "Cierres y efectivo del dia", _barberRows);
        var rightColumn = new StackPanel
        {
            Spacing = 14,
            Children =
            {
                CreateOperationPanel(),
                CreatePanel("Pagos recientes", "Ultimos cierres en autocaja", _paymentRows)
            }
        };

        Grid.SetColumn(rightColumn, 1);
        grid.Children.Add(barberPanel);
        grid.Children.Add(rightColumn);

        return grid;
    }

    private UIElement CreateOperationPanel()
    {
        var content = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                CreateMetricRow("Walk-ins", _walkInsText),
                CreateMetricRow("Citas", _appointmentsText),
                CreateMetricRow("Activos", _activeTurnsText),
                CreateMetricRow("No show / cancelados", _issuesText),
                _messageText
            }
        };

        _messageText.FontSize = 13;
        _messageText.Foreground = Brush(101, 108, 116);
        _messageText.TextWrapping = TextWrapping.Wrap;

        return CreatePanel("Operacion", "Resumen de turnos locales", content);
    }

    private static UIElement CreateMetricRow(string label, TextBlock valueText)
    {
        valueText.FontSize = 18;
        valueText.FontWeight = FontWeights.SemiBold;
        valueText.Foreground = Brush(30, 31, 34);

        return new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(),
                new ColumnDefinition { Width = GridLength.Auto }
            },
            Children =
            {
                new TextBlock
                {
                    Text = label,
                    FontSize = 14,
                    Foreground = Brush(101, 108, 116),
                    VerticalAlignment = VerticalAlignment.Center
                },
                PositionInColumn(valueText, 1)
            }
        };
    }

    private static Border CreatePanel(string title, string subtitle, UIElement content)
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

    private UIElement CreateOperationalNotes()
    {
        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());

        var cards = new[]
        {
            CreateNoteCard("\uE8FB", "Local", "Lee SQLite y no requiere internet."),
            CreateNoteCard("\uE8C7", "Efectivo", "Solo cierres registrados en autocaja."),
            CreateNoteCard("\uE9D2", "Comision", "Usa la comision guardada; el porcentaje final sigue pendiente.")
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
        LoadReport();
    }

    private void OnRefreshClick(object sender, RoutedEventArgs args)
    {
        LoadReport();
    }

    private void LoadReport()
    {
        try
        {
            var snapshot = _service.LoadDailyReport(_reportDatePicker.Date);
            ShowSnapshot(snapshot);
            SetStatus("Local", success: true);
        }
        catch (Exception exception)
        {
            ShowError(exception.Message);
        }
    }

    private void ShowSnapshot(LocalAdminReportSnapshot snapshot)
    {
        _checkInsText.Text = snapshot.Operations.CheckIns.ToString();
        _completedText.Text = snapshot.Operations.CompletedServices.ToString();
        _cashText.Text = FormatMoney(snapshot.Cash.TotalAmountCents, snapshot.Cash.Currency);
        _commissionText.Text = FormatMoney(snapshot.Cash.CommissionCents, snapshot.Cash.Currency);
        _walkInsText.Text = snapshot.Operations.WalkIns.ToString();
        _appointmentsText.Text = snapshot.Operations.Appointments.ToString();
        _activeTurnsText.Text = snapshot.Operations.ActiveTurns.ToString();
        _issuesText.Text = (snapshot.Operations.NoShows + snapshot.Operations.Cancelled).ToString();
        _lastRefreshText.Text = $"Actualizado: {snapshot.GeneratedAt:hh:mm tt}";
        _messageText.Text = snapshot.Cash.PaymentsMissingCommission == 0
            ? $"Pagos registrados: {snapshot.Cash.PaymentCount}. Cash drawer: {snapshot.Cash.CashDrawerOpenCount}."
            : $"Pagos registrados: {snapshot.Cash.PaymentCount}. Comisiones pendientes: {snapshot.Cash.PaymentsMissingCommission}.";

        ReplaceChildren(
            _barberRows,
            snapshot.Barbers.Select(row => CreateBarberRow(row, snapshot.Cash.Currency)),
            "Sin barberos registrados en la base local.");
        ReplaceChildren(
            _paymentRows,
            snapshot.RecentPayments.Select(CreatePaymentRow),
            "Sin pagos en efectivo para esta fecha.");
    }

    private static UIElement CreateBarberRow(BarberReportRow row, string currency)
    {
        return new Border
        {
            Background = row.ServicesClosed > 0 ? Brush(255, 255, 255) : Brush(248, 249, 251),
            BorderBrush = Brush(226, 230, 235),
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
                                Text = row.DisplayName,
                                FontSize = 18,
                                FontWeight = FontWeights.SemiBold,
                                Foreground = Brush(30, 31, 34)
                            },
                            new TextBlock
                            {
                                Text = $"{row.ServicesClosed} cierres - {FormatMoney(row.CommissionCents, currency)} comision",
                                FontSize = 13,
                                Foreground = Brush(101, 108, 116)
                            }
                        }
                    },
                    CreateTextBadge(FormatMoney(row.CashCollectedCents, currency), Brush(255, 247, 232), Brush(122, 82, 21))
                }
            }
        };
    }

    private static UIElement CreatePaymentRow(CashPaymentReportRow row)
    {
        var commissionText = row.CommissionCents is null
            ? "Comision pendiente"
            : $"{FormatMoney(row.CommissionCents.Value, row.Currency)} comision";

        return new Border
        {
            Background = Brush(255, 255, 255),
            BorderBrush = row.CommissionCents is null ? Brush(242, 181, 88) : Brush(226, 230, 235),
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
                                Text = $"{row.TicketNumber} - {row.BarberName}",
                                FontSize = 17,
                                FontWeight = FontWeights.SemiBold,
                                Foreground = Brush(30, 31, 34)
                            },
                            new TextBlock
                            {
                                Text = $"{row.CollectedAt:hh:mm tt} - {commissionText}",
                                FontSize = 13,
                                Foreground = Brush(101, 108, 116)
                            }
                        }
                    },
                    CreateTextBadge(FormatMoney(row.AmountCents, row.Currency), Brush(235, 248, 244), Brush(17, 105, 88))
                }
            }
        };
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

    private void ShowError(string message)
    {
        _checkInsText.Text = "0";
        _completedText.Text = "0";
        _cashText.Text = "USD 0.00";
        _commissionText.Text = "USD 0.00";
        _walkInsText.Text = "0";
        _appointmentsText.Text = "0";
        _activeTurnsText.Text = "0";
        _issuesText.Text = "0";
        _lastRefreshText.Text = "Sin datos actualizados";
        _messageText.Text = message;
        _barberRows.Children.Clear();
        _paymentRows.Children.Clear();
        _barberRows.Children.Add(CreateEmptyState("No se pudo leer la base local."));
        _paymentRows.Children.Add(CreateEmptyState(message));
        SetStatus("Error local", success: false);
    }

    private void SetStatus(string text, bool success)
    {
        _statusBadgeText.Text = text;
        _statusBadge.Background = success ? Brush(235, 248, 244) : Brush(255, 240, 238);
        _statusBadge.BorderBrush = success ? Brush(181, 224, 211) : Brush(231, 170, 162);
        _statusBadgeText.Foreground = success ? Brush(17, 105, 88) : Brush(154, 58, 47);
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

    private static T PositionInColumn<T>(T element, int column)
        where T : FrameworkElement
    {
        Grid.SetColumn(element, column);
        return element;
    }

    private static string FormatMoney(long cents, string currency)
    {
        return $"{currency} {Money.FromCents(cents):0.00}";
    }

    private static SolidColorBrush Brush(byte red, byte green, byte blue)
    {
        return new SolidColorBrush(ColorHelper.FromArgb(255, red, green, blue));
    }
}
