using System.Text;
using HealthTracker.Helpers;
using HealthTracker.Repositories;
using HealthTracker.Services;

namespace HealthTracker.Services;

public class AiChatService
{
    private readonly MedicineRepository _medicineRepo;
    private readonly MedicineScheduleRepository _scheduleRepo;
    private readonly MedicineLogRepository _logRepo;
    private readonly MedicineCourseRepository _courseRepo;
    private readonly DoctorVisitRepository _doctorRepo;
    private readonly MedicalReportRepository _reportRepo;
    private readonly ReportPageRepository _reportPageRepo;
    private readonly HealthMetricRepository _metricRepo;
    private readonly PrescriptionParserService _ocrService;
    private readonly PdfTextExtractorService _pdfExtractor;
    private readonly MedicineService _medicineService;

    public AiChatService(
        MedicineRepository medicineRepo,
        MedicineScheduleRepository scheduleRepo,
        MedicineLogRepository logRepo,
        MedicineCourseRepository courseRepo,
        DoctorVisitRepository doctorRepo,
        MedicalReportRepository reportRepo,
        ReportPageRepository reportPageRepo,
        HealthMetricRepository metricRepo,
        PrescriptionParserService ocrService,
        PdfTextExtractorService pdfExtractor,
        MedicineService medicineService)
    {
        _medicineRepo = medicineRepo;
        _scheduleRepo = scheduleRepo;
        _logRepo = logRepo;
        _courseRepo = courseRepo;
        _doctorRepo = doctorRepo;
        _reportRepo = reportRepo;
        _reportPageRepo = reportPageRepo;
        _metricRepo = metricRepo;
        _ocrService = ocrService;
        _pdfExtractor = pdfExtractor;
        _medicineService = medicineService;
    }

    public async Task<string> AskAsync(string question, List<(string Role, string Text)> history)
    {
        var key = AiParserService.GetApiKey();
        if (string.IsNullOrWhiteSpace(key))
            return "Gemini API key is not configured. Please go to Settings and enter your API key.";

        string context;
        try { context = await BuildContextAsync(); }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"BuildContext failed: {ex}");
            context = "(health data unavailable)";
        }

        var prompt = BuildFullPrompt(context, history, question);
        var result = await AiParserService.CallGemini(key, prompt, maxOutputTokens: 2048, temperature: 0.3);
        if (string.IsNullOrWhiteSpace(result)) return "Sorry, no response was received. Please try again.";
        if (result.StartsWith("__ERROR__", StringComparison.Ordinal))
        {
            System.Diagnostics.Debug.WriteLine($"AI chat failed: {result}");
            return "Could not reach the AI service. Check your internet connection and API key, then try again.";
        }
        return result;
    }

    private async Task<string> BuildContextAsync()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Today's date: {DateTime.Today:yyyy-MM-dd}");
        sb.AppendLine();

        // Active medicines
        var activeMeds = await _medicineRepo.GetAllActiveAsync();
        if (activeMeds.Count > 0)
        {
            sb.AppendLine("=== ACTIVE MEDICINES ===");
            foreach (var med in activeMeds)
            {
                var schedules = await _scheduleRepo.GetByMedicineIdAsync(med.Id);
                var (streak, missedThisWeek) = await _medicineService.GetAdherenceStats(med.Id);
                var course = await _courseRepo.GetActiveCourseAsync(med.Id);

                sb.AppendLine($"- {med.Name} ({med.StockUnit})");
                sb.AppendLine($"  Stock: {med.StockCount} {med.StockUnit}(s)");
                sb.AppendLine($"  Schedule: {string.Join(", ", schedules.Select(s => s.ScheduleTime))} ({schedules.Count}x daily)");
                sb.AppendLine($"  Start: {med.StartDate}  End: {med.EndDate ?? "Ongoing"}");
                if (!string.IsNullOrEmpty(med.Instructions)) sb.AppendLine($"  Instructions: {med.Instructions}");
                if (!string.IsNullOrEmpty(med.Notes)) sb.AppendLine($"  Notes: {med.Notes}");
                sb.AppendLine($"  Adherence: streak={streak} days, missed this week={missedThisWeek}");
                if (course != null)
                    sb.AppendLine($"  Course progress: {course.TotalTaken}/{(course.TotalScheduled > 0 ? course.TotalScheduled.ToString() : "∞")} doses");
            }
            sb.AppendLine();
        }

        // Past medicines
        var pastMeds = await _medicineRepo.GetAllInactiveAsync();
        if (pastMeds.Count > 0)
        {
            sb.AppendLine("=== PAST MEDICINES ===");
            foreach (var med in pastMeds)
            {
                var courses = await _courseRepo.GetByMedicineIdAsync(med.Id);
                var totalTaken = courses.Sum(c => c.TotalTaken);
                sb.AppendLine($"- {med.Name} ({med.StockUnit})");
                sb.AppendLine($"  Period: {med.StartDate} to {med.EndDate ?? "unknown"}");
                sb.AppendLine($"  Courses: {courses.Count}, Total doses taken: {totalTaken}");
                if (!string.IsNullOrEmpty(med.Notes)) sb.AppendLine($"  Notes: {med.Notes}");
            }
            sb.AppendLine();
        }

        // Medicine logs last 3 days (summarised per medicine per day)
        var from3d = DateTimeHelper.ToDateTimeString(DateTime.Today.AddDays(-3));
        var to3d = DateTimeHelper.ToDateTimeString(DateTime.Today.AddDays(1).AddSeconds(-1));
        var allMeds = activeMeds.Concat(pastMeds).ToList();
        var logLines = new List<string>();
        foreach (var med in allMeds)
        {
            var logs = await _logRepo.GetLogsForMedicineAsync(med.Id, from3d, to3d);
            var taken = logs.Count(l => l.WasTaken == 1);
            var missed = logs.Count(l => l.WasTaken == 0);
            if (logs.Count > 0)
                logLines.Add($"  {med.Name}: {taken} taken, {missed} missed (last 3 days)");
        }
        if (logLines.Count > 0)
        {
            sb.AppendLine("=== MEDICINE LOGS (last 3 days summary) ===");
            foreach (var line in logLines) sb.AppendLine(line);
            sb.AppendLine();
        }

        // Doctor visits (last 10)
        var visits = await _doctorRepo.GetAllOrderedAsync();
        if (visits.Count > 0)
        {
            sb.AppendLine("=== DOCTOR VISITS ===");
            foreach (var v in visits.Take(10))
            {
                sb.AppendLine($"- {v.VisitDate} — {v.DoctorName}" + (v.Hospital != null ? $" at {v.Hospital}" : ""));
                if (!string.IsNullOrEmpty(v.Diagnosis)) sb.AppendLine($"  Diagnosis: {v.Diagnosis}");
                if (!string.IsNullOrEmpty(v.Notes)) sb.AppendLine($"  Notes: {v.Notes}");
                if (!string.IsNullOrEmpty(v.FollowUpDate)) sb.AppendLine($"  Follow-up: {v.FollowUpDate}");
            }
            sb.AppendLine();
        }

        // Medical reports (last 5 — PDF full text truncated, images metadata only)
        var reports = await _reportRepo.GetAllOrderedAsync();
        var recentReports = reports.Take(5).ToList();
        if (recentReports.Count > 0)
        {
            sb.AppendLine("=== MEDICAL REPORTS ===");
            foreach (var r in recentReports)
            {
                sb.AppendLine($"- {r.ReportDate} — {r.Title} ({r.ReportType})" +
                              (r.DoctorName != null ? $" by {r.DoctorName}" : "") +
                              (r.Hospital != null ? $" at {r.Hospital}" : ""));
                if (!string.IsNullOrEmpty(r.Notes)) sb.AppendLine($"  Notes: {r.Notes}");

                // Only extract text from PDFs (images are too large via OCR)
                if (r.FileType.Equals("pdf", StringComparison.OrdinalIgnoreCase) && File.Exists(r.FilePath))
                {
                    var text = _pdfExtractor.ExtractText(r.FilePath);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        var truncated = text.Length > 1000 ? text[..1000] + "..." : text;
                        sb.AppendLine($"  Content: {truncated}");
                    }
                }
            }
            sb.AppendLine();
        }

        // Health metrics last 7 days (max 20 entries)
        var metrics = await _metricRepo.GetRecentAsync(20);
        var recentMetrics = metrics.Where(m =>
            DateTime.TryParse(m.RecordedAt, out var dt) && dt >= DateTime.Today.AddDays(-7)).ToList();
        if (recentMetrics.Count > 0)
        {
            sb.AppendLine("=== HEALTH METRICS (last 7 days) ===");
            foreach (var m in recentMetrics)
            {
                var val = m.SecondValue.HasValue
                    ? $"{m.Value}/{m.SecondValue} {m.Unit}"
                    : $"{m.Value} {m.Unit}";
                sb.AppendLine($"  {m.RecordedAt[..10]} — {m.MetricType}: {val}" +
                              (!string.IsNullOrEmpty(m.Notes) ? $" ({m.Notes})" : ""));
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private async Task<string> ExtractReportTextAsync(Models.MedicalReport report)
    {
        try
        {
            var pages = await _reportPageRepo.GetByReportIdAsync(report.Id);
            var sb = new StringBuilder();

            foreach (var page in pages.OrderBy(p => p.PageNumber))
            {
                if (!File.Exists(page.FilePath)) continue;

                if (page.FileType.Equals("pdf", StringComparison.OrdinalIgnoreCase))
                {
                    var text = _pdfExtractor.ExtractText(page.FilePath);
                    if (!string.IsNullOrWhiteSpace(text)) sb.AppendLine(text);
                }
                else
                {
                    // Image — use OCR
                    var bytes = await File.ReadAllBytesAsync(page.FilePath);
                    var text = await _ocrService.ExtractTextFromImage(bytes);
                    if (!string.IsNullOrWhiteSpace(text)) sb.AppendLine(text);
                }
            }

            return sb.ToString().Trim();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Report text extraction failed: {ex.Message}");
            return string.Empty;
        }
    }

    private static string BuildFullPrompt(string context,
        List<(string Role, string Text)> history, string question)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are a personal health assistant. The user's health data is provided below.");
        sb.AppendLine("Answer questions based on this data accurately and helpfully.");
        sb.AppendLine("Be concise. If data is missing, say so. Do not give medical diagnoses.");
        sb.AppendLine();
        sb.AppendLine("=== USER HEALTH DATA ===");
        sb.AppendLine(context);

        if (history.Count > 0)
        {
            sb.AppendLine("=== CONVERSATION HISTORY ===");
            foreach (var (role, text) in history)
                sb.AppendLine($"{(role == "user" ? "User" : "Assistant")}: {text}");
            sb.AppendLine();
        }

        sb.AppendLine("=== CURRENT QUESTION ===");
        sb.AppendLine(question);
        return sb.ToString();
    }

}
