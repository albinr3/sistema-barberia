using Barberia.Desktop.Shell;
using Barberia.Desktop.Views;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Barberia.Desktop;

public sealed partial class MainWindow : Window
{
    private readonly IReadOnlyDictionary<ShellModuleKey, ShellModuleDefinition> _modules;
    private readonly Dictionary<ShellModuleKey, Button> _moduleButtons = [];
    private ShellModuleKey? _currentModuleKey;

    public MainWindow()
    {
        _modules = ShellModuleCatalog.Modules.ToDictionary(module => module.Key);

        InitializeComponent();
        LoadBrandLogo();
        CreateNavigationButtons();
        SelectInitialModule();
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

    private void CreateNavigationButtons()
    {
        foreach (var module in ShellModuleCatalog.Modules)
        {
            if (module.Key == ShellModuleKey.PayrollHistory) continue;

            var button = CreateNavigationButton(module);
            _moduleButtons.Add(module.Key, button);
            _navigationItems.Children.Add(button);
        }
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
            Foreground = Brush(68, 70, 85),
            CornerRadius = new CornerRadius(8)
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
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock
                {
                    Text = module.Title,
                    FontSize = 14,
                    FontWeight = FontWeights.Medium
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

    internal void NavigateTo(ShellModuleKey moduleKey)
    {
        if (!_modules.TryGetValue(moduleKey, out var module))
        {
            return;
        }

        _header.Text = module.Title;
        _subtitle.Text = module.Subtitle;
        ApplyModuleChrome(moduleKey);

        if (_currentModuleKey == moduleKey)
        {
            return;
        }

        _contentHost.Content = CreateModulePage(module);
        _currentModuleKey = moduleKey;
        UpdateNavigationState(moduleKey);
    }

    private void ApplyModuleChrome(ShellModuleKey moduleKey)
    {
        var usesFullScreenChrome = moduleKey is ShellModuleKey.Kiosk or ShellModuleKey.PublicDisplay or ShellModuleKey.BarberPanel or ShellModuleKey.CashBox or ShellModuleKey.LocalAdmin or ShellModuleKey.Barbers or ShellModuleKey.Services or ShellModuleKey.TicketHistory or ShellModuleKey.Payroll or ShellModuleKey.PayrollHistory;
        _navigationColumn.Width = usesFullScreenChrome ? new GridLength(0) : new GridLength(256);
        _sidebar.Visibility = usesFullScreenChrome ? Visibility.Collapsed : Visibility.Visible;
        _moduleHeader.Visibility = usesFullScreenChrome ? Visibility.Collapsed : Visibility.Visible;
    }

    private Page CreateModulePage(ShellModuleDefinition module)
    {
        var page = Activator.CreateInstance(module.PageType) as Page
            ?? throw new InvalidOperationException($"Could not create shell page '{module.PageType.FullName}'.");

        if (page is KioskPage kioskPage)
        {
            kioskPage.ShellMenuRequested += (_, _) => ShowShellMenu();
        }
        else if (page is PublicDisplayPage publicDisplayPage)
        {
            publicDisplayPage.ShellMenuRequested += (_, _) => ShowShellMenu();
        }
        else if (page is BarberPanelPage barberPanelPage)
        {
            barberPanelPage.ShellMenuRequested += (_, _) => ShowShellMenu();
        }
        else if (page is CashBoxPage cashBoxPage)
        {
            cashBoxPage.ShellMenuRequested += (_, _) => ShowShellMenu();
        }
        else if (page is LocalAdminPage localAdminPage)
        {
            localAdminPage.ShellMenuRequested += (_, _) => ShowShellMenu();
        }
        else if (page is BarbersPage barbersPage)
        {
            barbersPage.ShellMenuRequested += (_, _) => ShowShellMenu();
        }
        else if (page is ServicesPage servicesPage)
        {
            servicesPage.ShellMenuRequested += (_, _) => ShowShellMenu();
        }
        else if (page is TicketHistoryPage ticketHistoryPage)
        {
            ticketHistoryPage.ShellMenuRequested += (_, _) => ShowShellMenu();
        }
        else if (page is PayrollPage payrollPage)
        {
            payrollPage.ShellMenuRequested += (_, _) => ShowShellMenu();
        }

        return page;
    }

    private void ShowShellMenu()
    {
        _navigationColumn.Width = new GridLength(256);
        _sidebar.Visibility = Visibility.Visible;
        _moduleHeader.Visibility = Visibility.Visible;
    }

    private void UpdateNavigationState(ShellModuleKey selectedModuleKey)
    {
        foreach (var (moduleKey, button) in _moduleButtons)
        {
            var isSelected = moduleKey == selectedModuleKey;
            button.Background = isSelected ? Brush(0, 32, 194) : new SolidColorBrush(Colors.Transparent);
            button.BorderBrush = new SolidColorBrush(Colors.Transparent);
            button.Foreground = isSelected ? Brush(152, 163, 255) : Brush(68, 70, 85);
        }
    }

    private static SolidColorBrush Brush(byte red, byte green, byte blue)
    {
        return new SolidColorBrush(ColorHelper.FromArgb(255, red, green, blue));
    }
}
