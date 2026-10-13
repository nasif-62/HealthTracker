using System.Collections.ObjectModel;
using System.Windows.Input;
using HealthTracker.Models;
using HealthTracker.Repositories;
using HealthTracker.Services;

namespace HealthTracker.ViewModels;

public class CourseSummaryRow
{
    public int MedicineId { get; set; }
    public string DateRange { get; set; } = string.Empty;
    public string SummaryLine { get; set; } = string.Empty;
    public double Progress { get; set; }
    public string ProgressText { get; set; } = string.Empty;
    public string ProgressColor => Progress >= 1.0 ? "#1A8A4A" : Progress > 0.5 ? "#1B5FA8" : "#E67E22";
}

public class MedicineHistoryGroup : List<CourseSummaryRow>
{
    public int MedicineId { get; set; }
    public string MedicineName { get; set; } = string.Empty;
    public int TotalDoses { get; set; }
    public string HeaderText => $"{MedicineName} ({TotalDoses} doses)";

    public MedicineHistoryGroup(int id, string name, int totalDoses, IEnumerable<CourseSummaryRow> items) : base(items)
    {
        MedicineId = id;
        MedicineName = name;
        TotalDoses = totalDoses;
    }
}

public class DoctorHistoryItem
{
    public int VisitId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public string? Hospital { get; set; }
    public string VisitDate { get; set; } = string.Empty;
    public string? Diagnosis { get; set; }
    public string VisitDateDisplay => DateTime.TryParse(VisitDate, out var d) ? d.ToString("MMM d, yyyy") : VisitDate;
    public string SubText => string.IsNullOrEmpty(Hospital) ? string.Empty : Hospital;
}

public class ReportHistoryItem
{
    public int ReportId { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string ReportType { get; set; } = string.Empty;
    public string ReportDate { get; set; } = string.Empty;
    public string? DoctorName { get; set; }
    public string ReportDateDisplay => DateTime.TryParse(ReportDate, out var d) ? d.ToString("MMM d, yyyy") : ReportDate;
    public string SubText => string.IsNullOrEmpty(DoctorName) ? ReportType : $"{ReportType} · {DoctorName}";
}

public class HistoryViewModel : BaseViewModel
{
    private readonly MedicineService _medicineService;
    private readonly MedicineCourseRepository _courseRepo;
    private readonly MedicineLogRepository _logRepo;
    private readonly MedicineRepository _medicineRepo;
    private readonly DoctorService _doctorService;
    private readonly ReportService _reportService;

    private string _category = "Medicine";
    public string Category
    {
        get => _category;
        set
        {
            _category = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(IsMedicine));
            OnPropertyChanged(nameof(IsDoctor));
            OnPropertyChanged(nameof(IsReports));
        }
    }
    public bool IsMedicine => Category == "Medicine";
    public bool IsDoctor => Category == "Doctor";
    public bool IsReports => Category == "Reports";

    public string Title => Category switch
    {
        "Medicine" => "Medicine History",
        "Doctor" => "Doctor History",
        "Reports" => "Reports History",
        _ => "History"
    };

    private DateTime _fromDate = DateTime.Today.AddMonths(-1);
    private DateTime _toDate = DateTime.Today;

    public DateTime FromDate
    {
        get => _fromDate;
        set { _fromDate = value; OnPropertyChanged(); }
    }

    public DateTime ToDate
    {
        get => _toDate;
        set { _toDate = value; OnPropertyChanged(); }
    }

    public ObservableCollection<MedicineHistoryGroup> MedicineGroups { get; } = new();
    public ObservableCollection<DoctorHistoryItem> DoctorItems { get; } = new();
    public ObservableCollection<ReportHistoryItem> ReportItems { get; } = new();

    public bool HasMedicineResults => MedicineGroups.Count > 0;
    public bool HasDoctorResults => DoctorItems.Count > 0;
    public bool HasReportResults => ReportItems.Count > 0;

    public ICommand SearchCommand { get; }

    public HistoryViewModel(MedicineService medicineService, MedicineCourseRepository courseRepo,
        MedicineLogRepository logRepo, MedicineRepository medicineRepo,
        DoctorService doctorService, ReportService reportService)
    {
        _medicineService = medicineService;
        _courseRepo = courseRepo;
        _logRepo = logRepo;
        _medicineRepo = medicineRepo;
        _doctorService = doctorService;
        _reportService = reportService;
        SearchCommand = new Command(async () => await Search());
    }

    public async Task Search()
    {
        if (IsBusy) return;
        IsBusy = true; ClearError();
        try
        {
            var from = FromDate.Date;
            var to = ToDate.Date.AddDays(1).AddSeconds(-1);

            if (IsMedicine) await LoadMedicineHistory(from, to);
            else if (IsDoctor) await LoadDoctorHistory(from, to);
            else if (IsReports) await LoadReportsHistory(from, to);
        }
        catch (Exception ex) { SetError("Failed to load history."); System.Diagnostics.Debug.WriteLine(ex); }
        finally { IsBusy = false; }
    }

    private async Task LoadMedicineHistory(DateTime from, DateTime to)
    {
        MedicineGroups.Clear();

        var allMedicines = await _medicineRepo.GetAllAsync();
        foreach (var med in allMedicines)
        {
            var courses = await _courseRepo.GetByMedicineIdAsync(med.Id);
            var courseRows = new List<CourseSummaryRow>();
            int totalDoses = 0;

            foreach (var course in courses)
            {
                if (!DateTime.TryParse(course.StartDate, out var cs)) continue;
                var ce = course.EndDate != null && DateTime.TryParse(course.EndDate, out var ced) ? ced : DateTime.MaxValue;

                // Check if course overlaps with date range
                if (cs.Date > to.Date || ce.Date < from.Date) continue;

                // Count logs in the overlapping window
                var overlapFrom = cs.Date > from.Date ? cs.Date : from.Date;
                var overlapTo = ce.Date < to.Date ? ce.Date : to.Date;

                var logs = await _logRepo.GetLogsForMedicineAsync(med.Id,
                    overlapFrom.ToString("yyyy-MM-ddT00:00:00", System.Globalization.CultureInfo.InvariantCulture),
                    overlapTo.ToString("yyyy-MM-ddT23:59:59", System.Globalization.CultureInfo.InvariantCulture));

                var taken = logs.Count(l => l.WasTaken == 1);
                var isActiveCourse = course.Status == "Active";

                // Skip completed courses with no doses in this period
                if (taken == 0 && !isActiveCourse) continue;

                totalDoses += taken;

                var startDisplay = cs.ToString("MMM d, yyyy");
                var endDisplay = course.EndDate != null ? ce.ToString("MMM d, yyyy") : "Ongoing";
                var perDay = course.SchedulesPerDay > 0 ? $"{course.SchedulesPerDay}x daily" : "";
                var days = course.PrescribedDays > 0 ? $"{course.PrescribedDays} days" : "Ongoing";
                var expected = course.TotalScheduled > 0 ? course.TotalScheduled : (taken > 0 ? taken : 1);
                var progress = taken == 0 ? 0.0 : Math.Min(1.0, (double)taken / expected);
                var progressText = isActiveCourse && taken == 0 ? "Active" : $"{(int)(progress * 100)}%";

                courseRows.Add(new CourseSummaryRow
                {
                    MedicineId = med.Id,
                    DateRange = $"{startDisplay} – {endDisplay}",
                    SummaryLine = taken == 0
                        ? $"{days} · {perDay} · In Progress"
                        : $"{days} · {perDay} · {taken}/{expected} doses",
                    Progress = progress,
                    ProgressText = progressText
                });
            }

            if (totalDoses > 0 || courseRows.Count > 0)
                MedicineGroups.Add(new MedicineHistoryGroup(med.Id, med.Name, totalDoses, courseRows));
        }

        OnPropertyChanged(nameof(HasMedicineResults));
    }

    private async Task LoadDoctorHistory(DateTime from, DateTime to)
    {
        DoctorItems.Clear();
        var visits = await _doctorService.GetAllVisits();
        foreach (var v in visits)
        {
            if (!DateTime.TryParse(v.VisitDate, out var vd)) continue;
            if (vd.Date < from.Date || vd.Date > to.Date) continue;
            DoctorItems.Add(new DoctorHistoryItem
            {
                VisitId = v.Id,
                DoctorName = v.DoctorName,
                Hospital = v.Hospital,
                VisitDate = v.VisitDate,
                Diagnosis = v.Diagnosis
            });
        }
        OnPropertyChanged(nameof(HasDoctorResults));
    }

    private async Task LoadReportsHistory(DateTime from, DateTime to)
    {
        ReportItems.Clear();
        var reports = await _reportService.GetAllReports();
        foreach (var r in reports)
        {
            if (!DateTime.TryParse(r.ReportDate, out var rd)) continue;
            if (rd.Date < from.Date || rd.Date > to.Date) continue;
            ReportItems.Add(new ReportHistoryItem
            {
                ReportId = r.Id,
                FilePath = r.FilePath,
                FileType = r.FileType,
                Title = r.Title,
                ReportType = r.ReportType,
                ReportDate = r.ReportDate,
                DoctorName = r.DoctorName
            });
        }
        OnPropertyChanged(nameof(HasReportResults));
    }
}
