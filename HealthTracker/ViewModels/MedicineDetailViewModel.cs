using System.Collections.ObjectModel;
using System.Windows.Input;
using HealthTracker.Helpers;
using HealthTracker.Models;
using HealthTracker.Services;

namespace HealthTracker.ViewModels;

public class ScheduleDisplayItem : BaseViewModel
{
    public int ScheduleId { get; set; }
    public int MedicineId { get; set; }
    public string ScheduleTime { get; set; } = string.Empty;
    public string ScheduleTimeDisplay => TimeSpan.TryParse(ScheduleTime, out var t)
        ? DateTime.Today.Add(t).ToString("h:mm tt") : ScheduleTime;

    private bool _isLogged;
    private bool _wasTaken;

    public bool IsLogged
    {
        get => _isLogged;
        set { _isLogged = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); OnPropertyChanged(nameof(StatusColor)); }
    }
    public bool WasTaken
    {
        get => _wasTaken;
        set { _wasTaken = value; OnPropertyChanged(); }
    }

    public string StatusText => IsLogged ? "Taken" : string.Empty;
    public string StatusColor => "#1A8A4A";
}

public class CourseDisplayItem
{
    public string DateRangeText { get; set; } = string.Empty;
    public string SummaryText { get; set; } = string.Empty;
    public string StatusIcon { get; set; } = string.Empty;
    public string StatusColor { get; set; } = "#5A7A9A";
}

public class MedicineDetailViewModel : BaseViewModel
{
    private readonly MedicineService _service;
    private readonly IReminderService _reminderService;
    private Medicine? _medicine;
    private bool _isInactive;

    public Medicine? Medicine { get => _medicine; set { _medicine = value; OnPropertyChanged(); } }
    public bool IsInactive { get => _isInactive; set { _isInactive = value; OnPropertyChanged(); } }
    public ObservableCollection<ScheduleDisplayItem> Schedules { get; } = new();
    public ObservableCollection<MedicineLog> RecentLogs { get; } = new();
    public ObservableCollection<CourseDisplayItem> Courses { get; } = new();

    public ICommand NewCourseCommand { get; }
    public ICommand BackfillPastDosesCommand { get; }

    public MedicineDetailViewModel(MedicineService service, IReminderService reminderService)
    {
        _service = service;
        _reminderService = reminderService;
        NewCourseCommand = new Command(async () => await StartNewCourse());
        BackfillPastDosesCommand = new Command(async () => await BackfillPastDoses());
    }

    public async Task Load(int medicineId)
    {
        IsBusy = true; ClearError();
        try
        {
            Medicine = await _service.GetMedicineById(medicineId);
            IsInactive = Medicine?.IsActive == 0;

            var schedules = await _service.GetSchedulesForMedicine(medicineId);
            var todayLogs = await _service.GetLogsForMedicine(medicineId, DateTime.Today, DateTime.Today.AddDays(1).AddSeconds(-1));

            Schedules.Clear();
            if (!IsInactive)
            {
                DateTime createdAt = default;
                var createdToday = Medicine != null
                    && DateTime.TryParse(Medicine.CreatedAt, out createdAt)
                    && createdAt.Date == DateTime.Today;

                foreach (var s in schedules)
                {
                    if (createdToday && TimeSpan.TryParse(s.ScheduleTime, out var schedTime) && schedTime < createdAt.TimeOfDay)
                        continue;

                    var log = todayLogs.FirstOrDefault(l => l.ScheduleId == s.Id);
                    Schedules.Add(new ScheduleDisplayItem
                    {
                        ScheduleId = s.Id,
                        MedicineId = s.MedicineId,
                        ScheduleTime = s.ScheduleTime,
                        IsLogged = log != null,
                        WasTaken = log?.WasTaken == 1
                    });
                }
            }

            var logs = await _service.GetLogsForMedicine(medicineId, DateTime.Today.AddDays(-7), DateTime.Today.AddDays(1).AddSeconds(-1));
            RecentLogs.Clear();
            foreach (var l in logs) RecentLogs.Add(l);

            // Load course history
            var courseList = await _service.GetCoursesForMedicine(medicineId);
            Courses.Clear();
            foreach (var c in courseList)
            {
                var startDisplay = DateTime.TryParse(c.StartDate, out var sd) ? sd.ToString("MMM d, yyyy") : c.StartDate;
                var endDisplay = DateTime.TryParse(c.EndDate, out var ed) ? ed.ToString("MMM d, yyyy") : (c.Status == "Active" ? "Ongoing" : "—");

                var dosesText = c.TotalScheduled > 0
                    ? $"{c.TotalTaken}/{c.TotalScheduled} doses taken"
                    : $"{c.TotalTaken} doses taken";

                var perDay = c.SchedulesPerDay > 0 ? $"{c.SchedulesPerDay}x daily" : "";
                var days = c.PrescribedDays > 0 ? $"{c.PrescribedDays} days" : "Ongoing";

                var summaryParts = new List<string>();
                if (!string.IsNullOrEmpty(days)) summaryParts.Add(days);
                if (!string.IsNullOrEmpty(perDay)) summaryParts.Add(perDay);
                summaryParts.Add(dosesText);

                string icon;
                string color;
                if (c.Status == "Active") { icon = "🟢"; color = "#1A8A4A"; }
                else if (c.TotalScheduled > 0 && c.TotalTaken >= c.TotalScheduled) { icon = "✅"; color = "#1A8A4A"; }
                else { icon = "⚠"; color = "#E67E22"; }

                Courses.Add(new CourseDisplayItem
                {
                    DateRangeText = $"{startDisplay} – {endDisplay}",
                    SummaryText = string.Join(" · ", summaryParts),
                    StatusIcon = icon,
                    StatusColor = color
                });
            }
        }
        catch (Exception ex) { SetError("Failed to load medicine details."); System.Diagnostics.Debug.WriteLine(ex); }
        finally { IsBusy = false; }
    }

    private async Task StartNewCourse()
    {
        if (Medicine is null) return;
        var result = await Shell.Current.DisplayPromptAsync(
            "Start New Course",
            "How many days did the doctor prescribe?",
            "Start", "Cancel",
            placeholder: "e.g. 5",
            keyboard: Keyboard.Numeric);

        if (string.IsNullOrWhiteSpace(result)) return;
        if (!int.TryParse(result, out var days) || days <= 0)
        {
            await Shell.Current.DisplayAlert("Invalid", "Please enter a valid number of days.", "OK");
            return;
        }

        IsBusy = true; ClearError();
        try
        {
            await _service.StartNewCourse(Medicine.Id, days, DateTimeHelper.ToDateString(DateTime.Today));
            await Load(Medicine.Id);
        }
        catch (Exception ex) { SetError("Failed to start new course."); System.Diagnostics.Debug.WriteLine(ex); }
        finally { IsBusy = false; }
    }

    private async Task BackfillPastDoses()
    {
        if (Medicine is null) return;
        if (!DateTime.TryParse(Medicine.StartDate, out var startDate)) return;

        if (startDate.Date >= DateTime.Today)
        {
            await Shell.Current.DisplayAlert("No Past Dates", "This medicine started today — no past doses to log.", "OK");
            return;
        }

        var pastDays = (DateTime.Today - startDate.Date).Days;
        var confirm = await Shell.Current.DisplayAlert(
            "Log Past Doses",
            $"Mark all doses from {startDate:MMM d} to yesterday ({pastDays} day(s)) as taken?",
            "Yes", "Cancel");

        if (!confirm) return;

        IsBusy = true; ClearError();
        try
        {
            var count = await _service.BackfillPastDoses(Medicine.Id, startDate, DateTime.Today.AddDays(-1));
            await Shell.Current.DisplayAlert("Done", $"{count} past dose(s) logged as taken.", "OK");
            await Load(Medicine.Id);
        }
        catch (Exception ex) { SetError("Failed to log past doses."); System.Diagnostics.Debug.WriteLine(ex); }
        finally { IsBusy = false; }
    }

}
