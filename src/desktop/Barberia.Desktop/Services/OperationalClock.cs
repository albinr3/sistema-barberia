namespace Barberia.Desktop.Services;

internal static class OperationalClock
{
    private static readonly Lazy<TimeZoneInfo> NewJerseyTimeZone = new(LoadNewJerseyTimeZone);

    public static DateTimeOffset Now => TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, NewJerseyTimeZone.Value);

    public static DateOnly Today => GetBusinessDate(Now);

    public static DateOnly GetBusinessDate(DateTimeOffset timestamp)
    {
        var newJerseyTime = TimeZoneInfo.ConvertTime(timestamp, NewJerseyTimeZone.Value);
        return DateOnly.FromDateTime(newJerseyTime.DateTime);
    }

    public static DateTimeOffset StartOfDay(DateOnly date)
    {
        var localMidnight = date.ToDateTime(TimeOnly.MinValue);
        var offset = NewJerseyTimeZone.Value.GetUtcOffset(localMidnight);
        return new DateTimeOffset(localMidnight, offset);
    }

    private static TimeZoneInfo LoadNewJerseyTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        }
    }
}
