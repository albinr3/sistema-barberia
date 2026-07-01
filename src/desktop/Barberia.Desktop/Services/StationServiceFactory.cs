namespace Barberia.Desktop.Services;

internal static class StationServiceFactory
{
    public static IKioskStationService CreateKioskService()
    {
        return StationRuntime.Current.Role == StationRole.KioskRotation
            ? new RemoteKioskStationService(StationRuntime.Current)
            : new KioskCheckInService();
    }

    public static IBarberRotationStationService CreateBarberRotationService()
    {
        return StationRuntime.Current.Role == StationRole.KioskRotation
            ? new RemoteBarberRotationStationService(StationRuntime.Current)
            : new LocalBarberRotationStationService();
    }

    public static ICashBoxStationService CreateCashBoxService()
    {
        return StationRuntime.Current.Role == StationRole.CashBox
            ? new RemoteCashBoxStationService(StationRuntime.Current)
            : new CashBoxCloseService();
    }
}
