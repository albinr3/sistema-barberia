using Barberia.Desktop.Services;
using Barberia.Desktop.Views;
using Microsoft.UI.Xaml;

namespace Barberia.Desktop;

public sealed partial class App : Application
{
    private readonly List<Window> _windows = [];
    private Window? _mainWindow;
    private DesktopSyncService? _desktopSyncService;
    private PayrollAutoPayService? _payrollAutoPayService;
    private DesktopBackupService? _desktopBackupService;
    private BarberiaLanServer? _lanServer;

    public static Window? MainWindowInstance { get; private set; }
    internal static DesktopBackupService? BackupServiceInstance { get; private set; }

    public App()
    {
        InitializeComponent();
        UnhandledException += OnUnhandledException;
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var settings = StationSettings.Load(Environment.GetCommandLineArgs());
        StationRuntime.Configure(settings);
        var startupPlan = StationStartupPlanner.Create(settings);

        if (startupPlan.RequiresLanHostBeforeOperation)
        {
            var health = await new LanHealthClient(settings).CheckAsync();
            if (!health.IsAvailable)
            {
                ShowBlockedWindow(settings, health);
                return;
            }
        }

        if (startupPlan.StartsDesktopBackgroundServices)
        {
            StartDesktopBackgroundServices();
        }

        if (startupPlan.StartsLanHost)
        {
            await StartLanServer(settings);
        }

        OpenStationWindows(startupPlan);
    }

    private void StartDesktopBackgroundServices()
    {
        _payrollAutoPayService = new PayrollAutoPayService();
        _payrollAutoPayService.Start();

        _desktopSyncService = new DesktopSyncService();
        _desktopSyncService.Start();

        _desktopBackupService = new DesktopBackupService();
        BackupServiceInstance = _desktopBackupService;
        _desktopBackupService.Start();
    }

    private async Task StartLanServer(StationSettings settings)
    {
        try
        {
            _lanServer = new BarberiaLanServer(settings);
            await _lanServer.StartAsync();
        }
        catch (Exception exception)
        {
            File.AppendAllText(
                LocalAppPaths.ErrorLogPath,
                $"[{OperationalClock.Now:O}] LAN host failed to start: {exception}{Environment.NewLine}");
            throw;
        }
    }

    private void OpenStationWindows(StationStartupPlan startupPlan)
    {
        if (startupPlan.Role is StationRole.Development or StationRole.OperationsHost)
        {
            _mainWindow = new MainWindow(startupPlan.MainModule, startupPlan.VisibleShellModules);
            MainWindowInstance = _mainWindow;
            TrackAndActivate(_mainWindow);
        }
        else
        {
            var primaryWindow = new ModuleWindow(startupPlan.MainModule);
            _mainWindow = primaryWindow;
            MainWindowInstance = primaryWindow;
            TrackAndActivate(primaryWindow);
        }

        foreach (var secondaryModule in startupPlan.SecondaryModules)
        {
            TrackAndActivate(new ModuleWindow(secondaryModule));
        }
    }

    private void ShowBlockedWindow(StationSettings settings, LanHealthCheckResult health)
    {
        var blockedWindow = new ConnectionBlockedWindow(settings, health);
        _mainWindow = blockedWindow;
        MainWindowInstance = blockedWindow;
        TrackAndActivate(blockedWindow);
    }

    private void TrackAndActivate(Window window)
    {
        _windows.Add(window);
        window.Closed += (_, _) => _windows.Remove(window);
        window.Activate();
    }

    private static void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs args)
    {
        try
        {
            File.AppendAllText(
                LocalAppPaths.ErrorLogPath,
                $"[{OperationalClock.Now:O}] {args.Exception}{Environment.NewLine}");
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
