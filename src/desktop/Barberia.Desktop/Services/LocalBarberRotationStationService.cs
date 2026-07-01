namespace Barberia.Desktop.Services;

internal sealed class LocalBarberRotationStationService : IBarberRotationStationService
{
    private readonly BarberCheckInService _checkInService = new();
    private readonly LocalAdminService _adminService = new();

    public BarberCheckInSnapshot Load()
    {
        return _checkInService.Load();
    }

    public BarberCheckInResult CheckIn(string stationInput)
    {
        return _checkInService.CheckIn(stationInput);
    }

    public void MarkBarberAvailable(Guid barberId)
    {
        _adminService.MarkBarberAvailable(barberId);
    }

    public void MarkBarberOffline(Guid barberId)
    {
        _adminService.MarkBarberOffline(barberId);
    }
}
