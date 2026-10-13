using SQLite;

namespace HealthTracker.Models;

[Table("MedicineSchedule")]
public class MedicineSchedule
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [NotNull]
    public int MedicineId { get; set; }

    /// <summary>Time of day in "HH:mm" format</summary>
    [NotNull]
    public string ScheduleTime { get; set; } = string.Empty;

    [NotNull]
    public int IsActive { get; set; } = 1;
}
