
using System.Globalization;

namespace Reaper.Exchanges.Binance.Services;
public static class TimeExtensions
{
    public static long ToUtcEpoch(this string dateStr)
    {
        var dateTime = DateTime.ParseExact(dateStr, "dd-MM-yyyy", CultureInfo.InvariantCulture);
        // Convert to UTC
        var utcDateTime = TimeZoneInfo.ConvertTimeToUtc(dateTime);

        // Convert to epoch time
        var epochTime = (long)(utcDateTime - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;

        return epochTime;
    }

    public static DateTime FromUtcEpoch(this long utcEpoch)
    {
        var seconds = utcEpoch / 1000;
        return DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime;
    }
}