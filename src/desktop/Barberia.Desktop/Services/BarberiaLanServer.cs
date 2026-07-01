using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Barberia.Data.Reports;
using Barberia.Hardware.Pos;
using Microsoft.AspNetCore.Http;

namespace Barberia.Desktop.Services;

internal sealed class BarberiaLanServer : IAsyncDisposable
{
    private readonly StationSettings _settings;
    private WebApplication? _app;

    public BarberiaLanServer(StationSettings settings)
    {
        _settings = settings;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_app is not null)
        {
            return;
        }

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls(_settings.LanListenUrl);
        var app = builder.Build();

        app.Use(async (context, next) =>
        {
            if (!LanApiAuthenticator.IsAuthorized(context.Request, _settings.LanSharedSecret))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new LanApiError("Unauthorized LAN station."), cancellationToken);
                return;
            }

            await next();
        });

        MapEndpoints(app, _settings);

        await app.StartAsync(cancellationToken);
        _app = app;
    }

    private static void MapEndpoints(WebApplication app, StationSettings settings)
    {
        app.MapGet("/health", () => new LanHealthResponse(
            LanApiContract.Version,
            Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0.0",
            Environment.MachineName,
            settings.Role.ToString(),
            OperationalClock.Now));

        app.MapGet("/api/kiosk/snapshot", () => Execute(() => new KioskCheckInService().Load()));
        app.MapPost("/api/kiosk/walk-ins", (LanKioskWalkInRequest request) => Execute(() =>
            new KioskCheckInService(
                LocalDesktopDatabase.CreateConnectionFactory(),
                new SimulatedKioskTicketPrinter()).RegisterWalkIn(
                request.CustomerName ?? string.Empty,
                request.AcceptsAnyBarber,
                request.RequestedBarberIds ?? [])));

        app.MapGet("/api/barber-rotation/snapshot", () => Execute(() => new BarberCheckInService().Load()));
        app.MapPost("/api/barber-rotation/check-ins", (LanStationInputRequest request) => Execute(() =>
            new BarberCheckInService().CheckIn(request.StationInput)));
        app.MapPost("/api/barber-rotation/barbers/available", (LanBarberStateRequest request) => Execute(() =>
        {
            new LocalAdminService().MarkBarberAvailable(request.BarberId);
            return new LanCommandResult("Barber marked available.");
        }));
        app.MapPost("/api/barber-rotation/barbers/offline", (LanBarberStateRequest request) => Execute(() =>
        {
            new LocalAdminService().MarkBarberOffline(request.BarberId);
            return new LanCommandResult("Barber marked offline.");
        }));

        app.MapGet("/api/public-display/snapshot", () => Execute(() => new PublicDisplaySnapshotService().Load()));
        app.MapGet("/api/appointments/snapshot", () => Execute(() => new LocalAppointmentsService().Load()));
        app.MapPost("/api/appointments/start-service", (LanStartServiceRequest request) => Execute(() =>
            new BarberPanelService().StartService(request.StationInput, request.ScannedTicketNumber)));

        app.MapGet("/api/cashbox/snapshot", () => Execute(() => CreateDeferredHardwareCashBoxService().Load()));
        app.MapPost("/api/cashbox/opening", (LanCashBoxOpeningRequest request) => Execute(() =>
            CreateDeferredHardwareCashBoxService().SaveOpeningBalance(request.OpeningBalance)));
        app.MapPost("/api/cashbox/ticket-lookup", (LanTicketInputRequest request) => Execute(() =>
            CreateDeferredHardwareCashBoxService().LookupTicket(request.TicketNumber)));
        app.MapGet("/api/cashbox/pending-payments", () => Execute(() =>
            CreateDeferredHardwareCashBoxService().ListPendingPayments()));
        app.MapPost("/api/cashbox/pending-payment-collector", (LanPendingPaymentCollectorRequest request) => Execute(() =>
            CreateDeferredHardwareCashBoxService().LookupPendingPaymentCollector(request.StationNumberInput)));
        app.MapPost("/api/cashbox/pay-later", (LanPendingPaymentRequest request) => Execute(() =>
            CreateDeferredHardwareCashBoxService().MarkServicePendingPayment(
                request.TicketNumber,
                request.ServiceId,
                request.AdditionalAmount)));
        app.MapPost("/api/cashbox/close-service", (LanCashBoxCloseServiceRequest request) => Execute(() =>
            CreateDeferredHardwareCashBoxService().CloseService(
                request.TicketNumber,
                request.ServiceId,
                request.AdditionalAmount,
                request.PaymentMethod,
                request.PaymentReference,
                request.TenderedAmount,
                request.ChangeAmount)));
        app.MapPost("/api/cashbox/collect-pending", (LanCollectPendingPaymentsRequest request) => Execute(() =>
            CreateDeferredHardwareCashBoxService().CollectPendingPayments(
                request.PendingPaymentIds,
                request.PaymentMethod,
                request.PaymentReference,
                request.CollectorStationNumber,
                request.TenderedAmount,
                request.ChangeAmount)));
        app.MapGet("/api/cashbox/day-report-job", () => Execute(BuildDayReportPrintJob));
        app.MapPost("/api/cashbox/day-report", () => Execute(() =>
        {
            _ = BuildDayReportPrintJob();
            return new LanCommandResult("Day report data is available for station printing.");
        }));

        app.MapPost("/api/hardware-events", (LanHardwareEventRequest request) => Execute(() =>
        {
            new HardwareAuditService().Record(request);
            return new LanCommandResult("Hardware event recorded.");
        }));
    }

    private static DayReportPrintJob BuildDayReportPrintJob()
    {
        var now = OperationalClock.Now;
        var deviceId = Environment.MachineName;
        var connectionFactory = LocalDesktopDatabase.CreateConnectionFactory();
        DailyOperationCoordinator.EnsureDailyReset(connectionFactory, now, deviceId);

        var businessDate = DailyOperationCoordinator.GetBusinessDate(now);
        var from = OperationalClock.StartOfDay(businessDate);
        var to = OperationalClock.StartOfDay(businessDate.AddDays(1));

        using var connection = connectionFactory.OpenConnection();
        var report = new LocalAdminReportRepository(connection).Load(from, to, now);
        if (!report.Cash.CashBoxOpened)
        {
            throw new InvalidOperationException("Open the cash box with today's opening cash before printing the day report.");
        }

        var barberReports = report.Barbers
            .Select(barber => new BarberDayReport(barber.DisplayNameWithStation, barber.ServicesClosed, barber.CashCollectedCents / 100m))
            .ToList();

        return new DayReportPrintJob(
            report.Cash.TotalSalesCents / 100m,
            report.Cash.OpeningBalanceCents / 100m,
            report.Cash.CashSalesCents / 100m,
            report.Cash.ZelleSalesCents / 100m,
            report.Cash.CashInDrawerCents / 100m,
            barberReports,
            now,
            deviceId);
    }
    private static CashBoxCloseService CreateDeferredHardwareCashBoxService()
    {
        return new CashBoxCloseService(
            LocalDesktopDatabase.CreateConnectionFactory(),
            new DeferredCashBoxReceiptPrinter(),
            new DeferredCashDrawer());
    }

    private static IResult Execute<T>(Func<T> action)
    {
        try
        {
            return Results.Ok(action());
        }
        catch (InvalidOperationException exception)
        {
            return Results.BadRequest(new LanApiError(exception.Message));
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }
}





