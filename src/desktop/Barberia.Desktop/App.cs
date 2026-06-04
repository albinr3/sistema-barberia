using Barberia.Desktop.Services;
using Microsoft.UI.Xaml;

namespace Barberia.Desktop;

public sealed partial class App : Application
{
    private Window? _mainWindow;

    public App()
    {
        InitializeComponent();
        UnhandledException += OnUnhandledException;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _mainWindow = new MainWindow();
        _mainWindow.Activate();
    }

    private static void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs args)
    {
        try
        {
            File.AppendAllText(
                LocalAppPaths.ErrorLogPath,
                $"[{DateTimeOffset.Now:O}] {args.Exception}{Environment.NewLine}");
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
