using SQLite;

namespace HealthTracker.Models;

[Table("HealthMetric")]
public class HealthMetric
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [NotNull]
    public string MetricType { get; set; } = string.Empty;

    [NotNull]
    public double Value { get; set; }

    public double? SecondValue { get; set; }

    [NotNull]
    public string Unit { get; set; } = string.Empty;

    [NotNull]
    public string RecordedAt { get; set; } = string.Empty;

    public string? Notes { get; set; }

    [Ignore]
    public string RecordedAtDisplay
    {
        get
        {
            if (!DateTime.TryParse(RecordedAt, out var dt)) return RecordedAt;
            return dt.TimeOfDay == TimeSpan.Zero
                ? dt.ToString("MMM d, yyyy")
                : dt.ToString("MMM d, yyyy h:mm tt");
        }
    }
}
