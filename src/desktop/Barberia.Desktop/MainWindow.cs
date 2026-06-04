using Barberia.Desktop.Shell;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Barberia.Desktop;

public sealed class MainWindow : Window
{
    private readonly IReadOnlyDictionary<ShellModuleKey, ShellModuleDefinition> _modules;
    private readonly ContentControl _contentHost = new();
    private readonly TextBlock _header = new();
    private readonly TextBlock _subtitle = new();
    private readonly Dictionary<ShellModuleKey, Button> _moduleButtons = [];
    private ShellModuleKey? _currentModuleKey;

    public MainWindow()
    {
        _modules = ShellModuleCatalog.Modules.ToDictionary(module => module.Key);

        Title = "Sistema Barberia";
        Content = CreateLayout();

        SelectInitialModule();
    }

    private Grid CreateLayout()
    {
        var root = new Grid
        {
            Background = Brush(248, 249, 251)
        };

        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(292) });
        root.ColumnDefinitions.Add(new ColumnDefinition());

        var navigationPane = new Border
        {
            Background = Brush(30, 29, 27),
            Child = CreateNavigationPane()
        };

        var mainContent = new Grid();
        mainContent.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        mainContent.RowDefinitions.Add(new RowDefinition());

        Grid.SetRow(_contentHost, 1);
        mainContent.Children.Add(CreateTopBar());
        mainContent.Children.Add(_contentHost);

        Grid.SetColumn(mainContent, 1);
        root.Children.Add(navigationPane);
        root.Children.Add(mainContent);

        return root;
    }

    private UIElement CreateTopBar()
    {
        var topBar = new Border
        {
            Background = Brush(255, 255, 255),
            BorderBrush = Brush(226, 230, 235),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(32, 22, 32, 18)
        };

        var layout = new Grid();
        layout.ColumnDefinitions.Add(new ColumnDefinition());
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var titleStack = new StackPanel
        {
            Spacing = 4
        };

        _header.FontSize = 28;
        _header.FontWeight = FontWeights.SemiBold;
        _header.Foreground = Brush(30, 31, 34);

        _subtitle.FontSize = 13;
        _subtitle.Foreground = Brush(101, 108, 116);

        titleStack.Children.Add(_header);
        titleStack.Children.Add(_subtitle);

        var status = new Border
        {
            Background = Brush(235, 248, 244),
            BorderBrush = Brush(181, 224, 211),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 8, 12, 8),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children =
                {
                    new FontIcon
                    {
                        Glyph = "\uE73E",
                        FontSize = 14,
                        Foreground = Brush(17, 127, 104)
                    },
                    new TextBlock
                    {
                        Text = "Fase 1 local",
                        FontSize = 13,
                        Foreground = Brush(17, 105, 88)
                    }
                }
            }
        };

        Grid.SetColumn(status, 1);
        layout.Children.Add(titleStack);
        layout.Children.Add(status);
        topBar.Child = layout;

        return topBar;
    }

    private Grid CreateNavigationPane()
    {
        var pane = new Grid
        {
            Padding = new Thickness(18, 22, 18, 18)
        };

        pane.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        pane.RowDefinitions.Add(new RowDefinition());
        pane.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var brand = new Grid
        {
            Margin = new Thickness(0, 0, 0, 28)
        };

        brand.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        brand.ColumnDefinitions.Add(new ColumnDefinition());

        var brandMark = new Border
        {
            Width = 42,
            Height = 42,
            CornerRadius = new CornerRadius(8),
            Background = Brush(242, 181, 88),
            Child = new FontIcon
            {
                Glyph = "\uE7C9",
                FontSize = 22,
                Foreground = Brush(30, 29, 27)
            }
        };

        var brandText = new StackPanel
        {
            Margin = new Thickness(12, 1, 0, 0),
            Spacing = 1,
            Children =
            {
                new TextBlock
                {
                    Text = "Barberia",
                    FontSize = 20,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brush(255, 255, 255)
                },
                new TextBlock
                {
                    Text = "Sistema local",
                    FontSize = 12,
                    Foreground = Brush(184, 181, 174)
                }
            }
        };

        Grid.SetColumn(brandText, 1);
        brand.Children.Add(brandMark);
        brand.Children.Add(brandText);

        var navigationItems = new StackPanel
        {
            Spacing = 8
        };

        foreach (var module in ShellModuleCatalog.Modules)
        {
            var button = CreateNavigationButton(module);
            _moduleButtons.Add(module.Key, button);
            navigationItems.Children.Add(button);
        }

        var footer = new Border
        {
            Background = Brush(42, 40, 37),
            BorderBrush = Brush(61, 58, 54),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14),
            Child = new StackPanel
            {
                Spacing = 3,
                Children =
                {
                    new TextBlock
                    {
                        Text = "Offline-first",
                        FontSize = 13,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = Brush(255, 255, 255)
                    },
                    new TextBlock
                    {
                        Text = "Operacion sin depender de internet",
                        FontSize = 12,
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = Brush(184, 181, 174)
                    }
                }
            }
        };

        Grid.SetRow(navigationItems, 1);
        Grid.SetRow(footer, 2);
        pane.Children.Add(brand);
        pane.Children.Add(navigationItems);
        pane.Children.Add(footer);

        return pane;
    }

    private Button CreateNavigationButton(ShellModuleDefinition module)
    {
        var button = new Button
        {
            Content = CreateNavigationButtonContent(module),
            Tag = module.Key,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(12, 10, 12, 10),
            Background = new SolidColorBrush(Colors.Transparent),
            BorderBrush = new SolidColorBrush(Colors.Transparent),
            Foreground = Brush(232, 230, 224)
        };

        ToolTipService.SetToolTip(button, module.Title);
        button.Click += OnNavigationButtonClick;
        return button;
    }

    private static UIElement CreateNavigationButtonContent(ShellModuleDefinition module)
    {
        var layout = new Grid
        {
            ColumnSpacing = 12
        };

        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        layout.ColumnDefinitions.Add(new ColumnDefinition());

        var icon = new FontIcon
        {
            Glyph = module.IconGlyph,
            FontSize = 17,
            Width = 24
        };

        var textStack = new StackPanel
        {
            Spacing = 1,
            Children =
            {
                new TextBlock
                {
                    Text = module.Title,
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold
                },
                new TextBlock
                {
                    Text = module.Subtitle,
                    FontSize = 11,
                    Opacity = 0.72
                }
            }
        };

        Grid.SetColumn(textStack, 1);
        layout.Children.Add(icon);
        layout.Children.Add(textStack);

        return layout;
    }

    private void SelectInitialModule()
    {
        NavigateTo(ShellModuleKey.Kiosk);
    }

    private void OnNavigationButtonClick(object sender, RoutedEventArgs args)
    {
        if (sender is not Button button ||
            button.Tag is not ShellModuleKey moduleKey)
        {
            return;
        }

        NavigateTo(moduleKey);
    }

    private void NavigateTo(ShellModuleKey moduleKey)
    {
        if (!_modules.TryGetValue(moduleKey, out var module))
        {
            return;
        }

        _header.Text = module.Title;
        _subtitle.Text = module.Subtitle;

        if (_currentModuleKey == moduleKey)
        {
            return;
        }

        _contentHost.Content = CreateModulePage(module);
        _currentModuleKey = moduleKey;
        UpdateNavigationState(moduleKey);
    }

    private static Page CreateModulePage(ShellModuleDefinition module)
    {
        return Activator.CreateInstance(module.PageType) as Page
            ?? throw new InvalidOperationException($"Could not create shell page '{module.PageType.FullName}'.");
    }

    private void UpdateNavigationState(ShellModuleKey selectedModuleKey)
    {
        foreach (var (moduleKey, button) in _moduleButtons)
        {
            var isSelected = moduleKey == selectedModuleKey;
            button.Background = isSelected ? Brush(255, 255, 255) : new SolidColorBrush(Colors.Transparent);
            button.BorderBrush = isSelected ? Brush(242, 181, 88) : new SolidColorBrush(Colors.Transparent);
            button.Foreground = isSelected ? Brush(30, 29, 27) : Brush(232, 230, 224);
        }
    }

    private static SolidColorBrush Brush(byte red, byte green, byte blue)
    {
        return new SolidColorBrush(ColorHelper.FromArgb(255, red, green, blue));
    }
}
