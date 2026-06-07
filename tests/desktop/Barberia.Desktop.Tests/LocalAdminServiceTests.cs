using System;
using System.Collections.Generic;
using Barberia.Core.Domain;
using Barberia.Data.Models;
using Barberia.Desktop.Services;
using Xunit;

namespace Barberia.Desktop.Tests;

public class LocalAdminServiceTests
{
    [Fact]
    public void CalculateAlerts_WithEmptyQueue_ReturnsNoAlerts()
    {
        var alerts = LocalAdminService.CalculateAlerts([], DateTimeOffset.Now, []);
        Assert.Empty(alerts);
    }

    [Fact]
    public void CalculateAlerts_WithWaitingMoreThan30Minutes_ReturnsWarningAlert()
    {
        var now = DateTimeOffset.Now;
        var turns = new[]
        {
            new ActiveTurnRow(
                new Turn(Guid.NewGuid(), "W1", 1, DateOnly.FromDateTime(now.DateTime), TurnState.Waiting, TurnSource.WalkIn, now.AddMinutes(-31), null, null, null, null, null, null, null),
                now.AddMinutes(-31))
        };

        var alerts = LocalAdminService.CalculateAlerts(turns, now, []);

        var alert = Assert.Single(alerts);
        Assert.Equal(AlertSeverity.Warning, alert.Severity);
        Assert.Equal(31, alert.ElapsedMinutes);
        Assert.Contains("waiting more than 30 minutes", alert.Title);
    }

    [Fact]
    public void CalculateAlerts_WithWaitingExactly30Minutes_ReturnsNoAlerts()
    {
        var now = DateTimeOffset.Now;
        var turns = new[]
        {
            new ActiveTurnRow(
                new Turn(Guid.NewGuid(), "W1", 1, DateOnly.FromDateTime(now.DateTime), TurnState.Waiting, TurnSource.WalkIn, now.AddMinutes(-30), null, null, null, null, null, null, null),
                now.AddMinutes(-30))
        };

        var alerts = LocalAdminService.CalculateAlerts(turns, now, []);

        Assert.Empty(alerts);
    }

    [Fact]
    public void CalculateAlerts_WithCalledMoreThan4Minutes_ReturnsCriticalAlert()
    {
        var now = DateTimeOffset.Now;
        var barberId = Guid.NewGuid();
        var turns = new[]
        {
            new ActiveTurnRow(
                new Turn(Guid.NewGuid(), "W1", 1, DateOnly.FromDateTime(now.DateTime), TurnState.Called, TurnSource.WalkIn, now.AddMinutes(-10), barberId, null, null, null, null, null, null),
                now.AddMinutes(-5))
        };
        var barbers = new[]
        {
            new Barber(barberId, "Marcus", BarberState.Called, 0, 1, null, 1, null, true)
        };

        var alerts = LocalAdminService.CalculateAlerts(turns, now, barbers);

        var alert = Assert.Single(alerts);
        Assert.Equal(AlertSeverity.Critical, alert.Severity);
        Assert.Equal(5, alert.ElapsedMinutes);
        Assert.Contains("not started", alert.Title);
        Assert.Contains("Marcus", alert.Detail);
    }

    [Fact]
    public void CalculateAlerts_WithInServiceOrCompleted_ReturnsNoAlerts()
    {
        var now = DateTimeOffset.Now;
        var barberId = Guid.NewGuid();
        var turns = new[]
        {
            new ActiveTurnRow(
                new Turn(Guid.NewGuid(), "W1", 1, DateOnly.FromDateTime(now.DateTime), TurnState.InService, TurnSource.WalkIn, now.AddMinutes(-10), barberId, null, null, null, now.AddMinutes(-5), null, null),
                now.AddMinutes(-5)),
            new ActiveTurnRow(
                new Turn(Guid.NewGuid(), "W2", 2, DateOnly.FromDateTime(now.DateTime), TurnState.Completed, TurnSource.WalkIn, now.AddMinutes(-10), barberId, null, null, null, now.AddMinutes(-5), now, null),
                now)
        };

        var alerts = LocalAdminService.CalculateAlerts(turns, now, []);

        Assert.Empty(alerts);
    }
}
