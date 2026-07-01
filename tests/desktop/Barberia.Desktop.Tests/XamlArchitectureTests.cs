using System.Text.RegularExpressions;
using Xunit;

namespace Barberia.Desktop.Tests;

public sealed class XamlArchitectureTests
{
    private static readonly Regex VisualClassPattern = new(
        @"^\s*public\s+(?<modifiers>(?:(?:sealed|abstract|partial)\s+)*)class\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*:\s*(?<base>[A-Za-z0-9_.]+)",
        RegexOptions.Compiled | RegexOptions.Multiline);

    [Fact]
    public void ConcreteWinUiWindowsAndPagesUseXamlPairs()
    {
        var desktopRoot = Path.Combine(FindRepositoryRoot(), "src", "desktop", "Barberia.Desktop");
        var visualClasses = new List<string>();

        foreach (var codeBehindPath in Directory.EnumerateFiles(desktopRoot, "*.cs", SearchOption.AllDirectories)
                     .Where(path => !IsBuildOutputPath(desktopRoot, path)))
        {
            var source = File.ReadAllText(codeBehindPath);

            foreach (Match match in VisualClassPattern.Matches(source))
            {
                var baseType = match.Groups["base"].Value;
                if (!IsWinUiVisualBase(baseType))
                {
                    continue;
                }

                var modifiers = match.Groups["modifiers"].Value;
                if (modifiers.Contains("abstract", StringComparison.Ordinal))
                {
                    continue;
                }

                var className = match.Groups["name"].Value;
                visualClasses.Add(className);

                var expectedXamlPath = Path.Combine(
                    Path.GetDirectoryName(codeBehindPath)!,
                    $"{className}.xaml");

                Assert.True(
                    File.Exists(expectedXamlPath),
                    $"{className} must have a sibling {className}.xaml file.");
                Assert.EndsWith(
                    $"{className}.xaml.cs",
                    codeBehindPath.Replace(Path.DirectorySeparatorChar, '/'),
                    StringComparison.Ordinal);
                Assert.Contains("partial", modifiers, StringComparison.Ordinal);
                Assert.Contains("InitializeComponent();", source, StringComparison.Ordinal);
            }
        }

        Assert.NotEmpty(visualClasses);
    }

    [Fact]
    public void PayrollUsesFullScreenChromeAndShellMenu()
    {
        var desktopRoot = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "desktop",
            "Barberia.Desktop");
        var mainWindowSource = File.ReadAllText(Path.Combine(desktopRoot, "MainWindow.xaml.cs"));
        var shellPageFactorySource = File.ReadAllText(Path.Combine(desktopRoot, "Shell", "ShellPageFactory.cs"));

        Assert.Contains("ShellModuleKey.Payroll", mainWindowSource, StringComparison.Ordinal);
        Assert.Contains("case PayrollPage payrollPage:", shellPageFactorySource, StringComparison.Ordinal);
        Assert.Contains("payrollPage.ShellMenuRequested += (_, _) => shellMenuRequested();", shellPageFactorySource, StringComparison.Ordinal);
    }

    [Fact]
    public void PublicDisplayRefreshesLocalSnapshotFrequently()
    {
        var publicDisplayPath = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "desktop",
            "Barberia.Desktop",
            "Views",
            "PublicDisplayPage.xaml.cs");
        var source = File.ReadAllText(publicDisplayPath);

        Assert.Contains("SnapshotRefreshInterval = TimeSpan.FromSeconds(5)", source, StringComparison.Ordinal);
        Assert.Contains("_refreshTimer.Interval = SnapshotRefreshInterval;", source, StringComparison.Ordinal);
    }

    [Fact]
    public void KioskDesktopLayoutTargetsTwelveBarbersWithoutScroll()
    {
        var kioskCodePath = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "desktop",
            "Barberia.Desktop",
            "Views",
            "KioskPage.xaml.cs");
        var source = File.ReadAllText(kioskCodePath);

        Assert.Contains("width < 1460 ? 4 : 5", source, StringComparison.Ordinal);
        Assert.Contains("_contentCanvas.Height = denseDesktop ? availableHeight : double.NaN;", source, StringComparison.Ordinal);
        Assert.Contains("_screenScrollViewer.VerticalScrollBarVisibility = denseDesktop ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;", source, StringComparison.Ordinal);
        Assert.Contains("denseDesktop ? 84", source, StringComparison.Ordinal);
        Assert.DoesNotContain("MinHeight = 224", source, StringComparison.Ordinal);
    }

    private static bool IsWinUiVisualBase(string baseType)
    {
        return baseType is "Window" or "Page" or "Microsoft.UI.Xaml.Window" or "Microsoft.UI.Xaml.Controls.Page";
    }

    private static bool IsBuildOutputPath(string root, string path)
    {
        var relativePath = Path.GetRelativePath(root, path);
        var segments = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return segments.Contains("bin", StringComparer.OrdinalIgnoreCase) ||
            segments.Contains("obj", StringComparer.OrdinalIgnoreCase);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "BarberiaSystem.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root from test output directory.");
    }
}



