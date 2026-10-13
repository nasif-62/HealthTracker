using System.Globalization;
using Android.Content;
using SQLite;

namespace HealthTracker;

public class WidgetDataRecord
{
    public string ProgressText { get; set; } = "-- of -- doses today";
    public string NextDoseText { get; set; } = "";
    public string HeartRateText { get; set; } = "♥ --";
    public string BloodPressureText { get; set; } = "BP --";
}

public static class WidgetDataHelper
{
    public static WidgetDataRecord GetWidgetData(Context context)
    {
        var dbPath = System.IO.Path.Combine(context.FilesDir!.AbsolutePath, "healthapp.db");
        if (!System.IO.File.Exists(dbPath))
            return new WidgetDataRecord { ProgressText = "No data yet", NextDoseText = "Open app to add medicines" };

        SQLiteConnection? db = null;
        try
        {
            db = new SQLiteConnection(dbPath, SQLiteOpenFlags.ReadOnly);

            // The app writes all dates/times with the invariant culture; compare with the same format,
            // otherwise devices with a non-Gregorian calendar or a '.' time separator never match.
            var inv = CultureInfo.InvariantCulture;
            var todayStr = DateTime.Today.ToString("yyyy-MM-dd", inv);
            var tomorrowStr = DateTime.Today.AddDays(1).ToString("yyyy-MM-dd", inv);
            var nowTime = DateTime.Now.ToString("HH:mm", inv);
            var nowTimeOfDay = DateTime.Now.TimeOfDay;

            var medicines = db.Query<MRow>("SELECT Id, CreatedAt FROM Medicine WHERE IsActive = 1");
            if (medicines.Count == 0)
                return new WidgetDataRecord { ProgressText = "No active medicines", NextDoseText = "Add medicines in the app" };

            var idList = string.Join(",", medicines.Select(m => m.Id));

            var allSchedules = db.Query<SRow>(
                $"SELECT Id, MedicineId, ScheduleTime FROM MedicineSchedule WHERE MedicineId IN ({idList}) AND IsActive = 1");

            // Match the app's Today page: for a medicine added today, slots that had already passed
            // at creation time are not part of today's plan.
            var createdTodayAt = medicines
                .Where(m => DateTime.TryParse(m.CreatedAt, inv, DateTimeStyles.None, out var c) && c.Date == DateTime.Today)
                .ToDictionary(m => m.Id, m => DateTime.Parse(m.CreatedAt, inv).TimeOfDay);
            var schedules = allSchedules
                .Where(s => !createdTodayAt.TryGetValue(s.MedicineId, out var created)
                            || !TimeSpan.TryParse(s.ScheduleTime, inv, out var t)
                            || t >= created)
                .ToList();

            var scheduleIds = schedules.Select(s => s.Id).ToHashSet();

            var todayLogs = db.Query<LRow>(
                $"SELECT ScheduleId, WasTaken FROM MedicineLog WHERE MedicineId IN ({idList}) AND ScheduledFor >= ? AND ScheduledFor < ?",
                todayStr, tomorrowStr);

            // Only count logs for schedules that are part of today's plan, so taken never exceeds total.
            var taken = todayLogs.Count(l => l.WasTaken == 1 && scheduleIds.Contains(l.ScheduleId));
            var loggedIds = todayLogs.Select(l => l.ScheduleId).ToHashSet();

            var total = schedules.Count;
            var progressText = total > 0 ? $"{taken} of {total} doses taken today" : "No schedules set";

            var nextSchedule = schedules
                .Where(s => !loggedIds.Contains(s.Id)
                            && TimeSpan.TryParse(s.ScheduleTime, inv, out var t)
                            && t > nowTimeOfDay)
                .OrderBy(s => s.ScheduleTime)
                .FirstOrDefault();

            string nextDoseText;
            if (nextSchedule != null)
            {
                var medName = db.Query<NRow>("SELECT Name FROM Medicine WHERE Id = ?", nextSchedule.MedicineId)
                    .FirstOrDefault()?.Name ?? "Medicine";
                nextDoseText = $"Next: {medName} at {nextSchedule.ScheduleTime}";
            }
            else
            {
                nextDoseText = taken >= total && total > 0 ? "✅ All doses taken today!" : "No upcoming doses";
            }

            var hr = db.Query<VRow>("SELECT Value FROM HealthMetric WHERE MetricType = 'HeartRate' ORDER BY RecordedAt DESC LIMIT 1");
            var hrText = hr.Count > 0 ? $"♥ {hr[0].Value:0} bpm" : "♥ --";

            var bp = db.Query<VRow2>("SELECT Value, SecondValue FROM HealthMetric WHERE MetricType = 'BloodPressure' ORDER BY RecordedAt DESC LIMIT 1");
            var bpText = bp.Count > 0 ? $"BP {bp[0].Value:0}/{bp[0].SecondValue:0}" : "BP --";

            return new WidgetDataRecord
            {
                ProgressText = progressText,
                NextDoseText = nextDoseText,
                HeartRateText = hrText,
                BloodPressureText = bpText
            };
        }
        catch (Exception ex)
        {
            Android.Util.Log.Error("HealthWidget", $"DB error: {ex.Message}");
            return new WidgetDataRecord { ProgressText = "Tap to open app", NextDoseText = "" };
        }
        finally
        {
            db?.Close();
        }
    }

    private class MRow { public int Id { get; set; } public string CreatedAt { get; set; } = ""; }
    private class SRow { public int Id { get; set; } public int MedicineId { get; set; } public string ScheduleTime { get; set; } = ""; }
    private class LRow { public int ScheduleId { get; set; } public int WasTaken { get; set; } }
    private class NRow { public string Name { get; set; } = ""; }
    private class VRow { public double Value { get; set; } }
    private class VRow2 { public double Value { get; set; } public double? SecondValue { get; set; } }
}
