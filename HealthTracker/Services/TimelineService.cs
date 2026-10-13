using HealthTracker.Helpers;
using HealthTracker.Models;
using HealthTracker.Repositories;

namespace HealthTracker.Services;

public class TimelineService
{
    private readonly DoctorVisitRepository _visitRepo;
    private readonly MedicalReportRepository _reportRepo;
    private readonly HealthMetricRepository _metricRepo;
    private readonly MedicineLogRepository _logRepo;
    private readonly MedicineRepository _medicineRepo;
    private readonly MedicineCourseRepository _courseRepo;

    public TimelineService(DoctorVisitRepository visitRepo, MedicalReportRepository reportRepo,
        HealthMetricRepository metricRepo, MedicineLogRepository logRepo, MedicineRepository medicineRepo,
        MedicineCourseRepository courseRepo)
    {
        _visitRepo = visitRepo;
        _reportRepo = reportRepo;
        _metricRepo = metricRepo;
        _logRepo = logRepo;
        _medicineRepo = medicineRepo;
        _courseRepo = courseRepo;
    }

    public async Task<List<TimelineEntry>> GetTimeline(int pageSize = 20, int offset = 0)
    {
        var entries = new List<TimelineEntry>();

        var visits = await _visitRepo.GetAllOrderedAsync();
        foreach (var v in visits)
        {
            var dateOnly = v.CreatedAt.Length >= 10 ? v.CreatedAt[..10] : v.CreatedAt;
            entries.Add(new TimelineEntry
            {
                SourceId = v.Id,
                EventType = "doctor_visit",
                DisplayLabel = "Doctor Visit",
                Description = $"{v.DoctorName}{(v.Hospital != null ? $" — {v.Hospital}" : "")}",
                EventDate = dateOnly,
                SortKey = v.CreatedAt ?? dateOnly,
                TimeText = ParseTimeAmPm(v.CreatedAt),
                SubText = $"Visit date: {v.VisitDate}",
                MonthYearGroup = DateTimeHelper.ToMonthYearGroup(dateOnly),
                TagColor = "#1B5FA8",
                TagIcon = "🏥"
            });
        }

        var reports = await _reportRepo.GetAllOrderedAsync();
        foreach (var r in reports)
        {
            var dateOnly = r.CreatedAt.Length >= 10 ? r.CreatedAt[..10] : r.CreatedAt;
            entries.Add(new TimelineEntry
            {
                SourceId = r.Id,
                EventType = "report_added",
                DisplayLabel = "Report",
                Description = $"{r.Title} ({r.ReportType})",
                EventDate = dateOnly,
                SortKey = r.CreatedAt ?? dateOnly,
                TimeText = ParseTimeAmPm(r.CreatedAt),
                MonthYearGroup = DateTimeHelper.ToMonthYearGroup(dateOnly),
                TagColor = "#7B2FBE",
                TagIcon = "📄"
            });
        }

        var metrics = await _metricRepo.GetRecentAsync(200);
        foreach (var m in metrics)
        {
            var dateOnly = m.RecordedAt.Length >= 10 ? m.RecordedAt[..10] : m.RecordedAt;
            entries.Add(new TimelineEntry
            {
                SourceId = m.Id,
                EventType = "metric_recorded",
                DisplayLabel = "Health Metric",
                Description = $"{m.MetricType}: {m.Value}{(m.SecondValue.HasValue ? $"/{m.SecondValue}" : "")} {m.Unit}",
                EventDate = dateOnly,
                SortKey = m.RecordedAt ?? dateOnly,
                TimeText = ParseTimeAmPm(m.RecordedAt),
                MonthYearGroup = DateTimeHelper.ToMonthYearGroup(dateOnly),
                TagColor = "#1A8A4A",
                TagIcon = "📊"
            });
        }

        var logs = await _logRepo.GetRecentLogsAsync(30);
        foreach (var l in logs)
        {
            var medicine = await _medicineRepo.GetByIdAsync(l.MedicineId);
            var medicineName = medicine?.Name ?? "Unknown";
            var dateOnly = l.ScheduledFor.Length >= 10 ? l.ScheduledFor[..10] : l.ScheduledFor;
            var taken = l.WasTaken == 1;
            entries.Add(new TimelineEntry
            {
                SourceId = l.Id,
                EventType = taken ? "dose_taken" : "dose_missed",
                DisplayLabel = taken ? "Dose Taken" : "Dose Missed",
                Description = medicineName,
                EventDate = dateOnly,
                SortKey = l.ScheduledFor ?? dateOnly,
                TimeText = ParseTimeAmPm(l.ScheduledFor),
                MonthYearGroup = DateTimeHelper.ToMonthYearGroup(dateOnly),
                TagColor = taken ? "#1A8A4A" : "#C0392B",
                TagIcon = taken ? "✅" : "❌"
            });
        }

        var medicines = await _medicineRepo.GetAllAsync();
        foreach (var med in medicines)
        {
            var createdDateOnly = med.CreatedAt.Length >= 10 ? med.CreatedAt[..10] : med.CreatedAt;
            entries.Add(new TimelineEntry
            {
                SourceId = med.Id,
                EventType = "medicine_added",
                DisplayLabel = "Medicine Added",
                Description = med.Name,
                EventDate = createdDateOnly,
                SortKey = med.CreatedAt ?? createdDateOnly,
                TimeText = ParseTimeAmPm(med.CreatedAt),
                MonthYearGroup = DateTimeHelper.ToMonthYearGroup(createdDateOnly),
                TagColor = "#E67E22",
                TagIcon = "💊"
            });
        }

        // Completed courses
        var allCourses = await _courseRepo.GetAllAsync();
        foreach (var c in allCourses.Where(c => c.Status == "Completed" && !string.IsNullOrEmpty(c.EndDate)))
        {
            var medicine = await _medicineRepo.GetByIdAsync(c.MedicineId);
            var medName = medicine?.Name ?? "Unknown";
            var dateOnly = c.EndDate!.Length >= 10 ? c.EndDate[..10] : c.EndDate;
            entries.Add(new TimelineEntry
            {
                SourceId = c.Id,
                EventType = "course_completed",
                DisplayLabel = "Course Completed",
                Description = medName,
                EventDate = dateOnly,
                SortKey = c.EndDate,
                TimeText = null,
                MonthYearGroup = DateTimeHelper.ToMonthYearGroup(dateOnly),
                TagColor = "#1A8A4A",
                TagIcon = "✅"
            });
        }

        return entries
            .OrderByDescending(e => e.SortKey)
            .Skip(offset)
            .Take(pageSize)
            .ToList();
    }

    private static string? ParseTimeAmPm(string? dateTimeStr)
    {
        if (string.IsNullOrEmpty(dateTimeStr)) return null;
        if (DateTime.TryParse(dateTimeStr, out var dt) && dt.TimeOfDay != TimeSpan.Zero)
            return dt.ToString("h:mm tt");
        // Try parsing just a time component like "HH:mm"
        if (dateTimeStr.Length == 5 && TimeSpan.TryParse(dateTimeStr, out var ts))
            return DateTime.Today.Add(ts).ToString("h:mm tt");
        return null;
    }
}
