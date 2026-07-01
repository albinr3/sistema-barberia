using Barberia.Desktop.Services;
using Barberia.Desktop.Shell;
using Xunit;

namespace Barberia.Desktop.Tests;

public sealed class StationStartupPlannerTests
{
    [Fact]
    public void ParseRoleArgument_SupportsEqualsAndSeparateValueForms()
    {
        Assert.Equal(StationRole.KioskRotation, StationSettings.TryParseRoleArgument(["--station=KioskRotation"]));
        Assert.Equal(StationRole.CashBox, StationSettings.TryParseRoleArgument(["--station", "CashBox"]));
        Assert.Equal(StationRole.OperationsHost, StationSettings.TryParseRoleArgument(["--station", "operationshost"]));
    }

    [Fact]
    public void DevelopmentPlan_PreservesSingleWindowLocalWorkflow()
    {
        var plan = StationStartupPlanner.Create(CreateSettings(StationRole.Development));

        Assert.True(plan.StartsDesktopBackgroundServices);
        Assert.False(plan.StartsLanHost);
        Assert.False(plan.RequiresLanHostBeforeOperation);
        Assert.Equal(ShellModuleKey.Kiosk, plan.MainModule);
        Assert.Empty(plan.SecondaryModules);
        Assert.Null(plan.VisibleShellModules);
    }

    [Fact]
    public void KioskRotationPlan_OpensKioskAndRotationWithoutBackgroundServices()
    {
        var plan = StationStartupPlanner.Create(CreateSettings(StationRole.KioskRotation));

        Assert.False(plan.StartsDesktopBackgroundServices);
        Assert.False(plan.StartsLanHost);
        Assert.True(plan.RequiresLanHostBeforeOperation);
        Assert.Equal(ShellModuleKey.Kiosk, plan.MainModule);
        Assert.Equal([ShellModuleKey.BarberRotation], plan.SecondaryModules);
    }

    [Fact]
    public void CashBoxPlan_BlocksUntilOperationsHostIsReachable()
    {
        var plan = StationStartupPlanner.Create(CreateSettings(StationRole.CashBox));

        Assert.False(plan.StartsDesktopBackgroundServices);
        Assert.True(plan.RequiresLanHostBeforeOperation);
        Assert.Equal(ShellModuleKey.CashBox, plan.MainModule);
        Assert.Empty(plan.SecondaryModules);
    }

    [Fact]
    public void OperationsHostPlan_StartsLanHostAndKeepsAdminModulesAvailable()
    {
        var plan = StationStartupPlanner.Create(CreateSettings(StationRole.OperationsHost));

        Assert.True(plan.StartsDesktopBackgroundServices);
        Assert.True(plan.StartsLanHost);
        Assert.False(plan.RequiresLanHostBeforeOperation);
        Assert.Equal(ShellModuleKey.PublicDisplay, plan.MainModule);
        Assert.Equal([ShellModuleKey.Appointments], plan.SecondaryModules);
        Assert.Contains(ShellModuleKey.LocalAdmin, plan.VisibleShellModules!);
        Assert.DoesNotContain(ShellModuleKey.Kiosk, plan.VisibleShellModules!);
    }

    private static StationSettings CreateSettings(StationRole role)
    {
        return new StationSettings(
            role,
            StationSettings.DefaultLanServerUrl,
            StationSettings.DefaultLanListenUrl,
            "TEST-PC",
            null,
            StartLanHostInDevelopment: false);
    }
}
