using System.Globalization;

namespace HealthTracker.Helpers;

public static class DateTimeHelper
{
    public static string ToDateString(DateTime dt) =>
        dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string ToDateTimeString(DateTime dt) =>
        dt.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);

    public static DateTime ParseDate(string s) =>
        DateTime.Parse(s, CultureInfo.InvariantCulture);

    public static DateTime ParseDateTime(string s) =>
        DateTime.Parse(s, CultureInfo.InvariantCulture);

    /// <summary>Culture-invariant TryParse for the date and date-time strings this app stores.</summary>
    public static bool TryParse(string? s, out DateTime result)
    {
        if (string.IsNullOrWhiteSpace(s)) { result = default; return false; }
        return DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
    }

    public static string ToMonthYearGroup(string dateStr)
    {
        if (!TryParse(dateStr, out var dt)) return "Unknown";
        return dt.ToString("MMMM yyyy", CultureInfo.InvariantCulture);
    }
}
