using Barberia.Desktop.Services;
using Microsoft.UI.Xaml;

namespace Barberia.Desktop;

public sealed partial class App : Application
{
    private Window? _mainWindow;
    private DesktopSyncService? _desktopSyncService;
    private PayrollAutoPayService? _payrollAutoPayService;
    private DesktopBackupService? _desktopBackupService;

    public static Window? MainWindowInstance { get; private set; }
    internal static DesktopBackupService? BackupServiceInstance { get; private set; }

    public App()
    {
        InitializeComponent();
        UnhandledException += OnUnhandledException;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _payrollAutoPayService = new PayrollAutoPayService();
        _payrollAutoPayService.Start();

        _desktopSyncService = new DesktopSyncService();
        _desktopSyncService.Start();

        _desktopBackupService = new DesktopBackupService();
        BackupServiceInstance = _desktopBackupService;
        _desktopBackupService.Start();

        _mainWindow = new MainWindow();
        MainWindowInstance = _mainWindow;
        _mainWindow.Activate();
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
