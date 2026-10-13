using System.Collections.ObjectModel;
using System.Windows.Input;
using HealthTracker.Services;

namespace HealthTracker.ViewModels;

public class TodayViewModel : BaseViewModel
{
    private readonly MedicineService _medicineService;
    private readonly IReminderService _reminderService;
    private int _takenDoses;
    private int _totalDoses;

    public ObservableCollection<TodayScheduleItem> Items { get; } = new();
    public string TodayDate => DateTime.Today.ToString("dddd, MMMM d");

    public int TakenDoses
    {
        get => _takenDoses;
        private set { _takenDoses = value; OnPropertyChanged(); OnPropertyChanged(nameof(DayProgress)); OnPropertyChanged(nameof(SummaryText)); }
    }

    public int TotalDoses
    {
        get => _totalDoses;
        private set { _totalDoses = value; OnPropertyChanged(); OnPropertyChanged(nameof(DayProgress)); OnPropertyChanged(nameof(SummaryText)); }
    }

    public double DayProgress => TotalDoses > 0 ? Math.Min(1.0, (double)TakenDoses / TotalDoses) : 0;

    public string SummaryText => TotalDoses == 0
        ? "No doses scheduled today"
        : $"{TakenDoses} of {TotalDoses} doses completed";

    public ICommand LoadCommand { get; }
    public ICommand TakeDoseCommand { get; }

    public TodayViewModel(MedicineService medicineService, IReminderService reminderService)
    {
        _medicineService = medicineService;
        _reminderService = reminderService;
        LoadCommand = new Command(async () => await Load());
        TakeDoseCommand = new Command<TodayScheduleItem>(async item => await TakeDose(item));
    }

    public async Task Load()
    {
        if (IsBusy) return;
        IsBusy = true; ClearError();
        OnPropertyChanged(nameof(TodayDate)); // the Today tab is reused across days
        try
        {
            await _medicineService.AutoLogMissedDoses();
            await _medicineService.CheckAndCompleteCourses();
            var medicines = await _medicineService.GetAllActiveMedicines();
            var allItems = new List<TodayScheduleItem>();

            foreach (var med in medicines)
            {
                var schedules = await _medicineService.GetSchedulesForMedicine(med.Id);
                var todayLogs = await _medicineService.GetLogsForMedicine(
                    med.Id, DateTime.Today, DateTime.Today.AddDays(1).AddSeconds(-1));

                var createdToday = DateTime.TryParse(med.CreatedAt, out var createdAt) && createdAt.Date == DateTime.Today;
                var now = DateTime.Now.TimeOfDay;

                foreach (var sched in schedules)
                {
                    // If medicine was added today, skip schedule slots that already passed at creation time
                    if (createdToday && TimeSpan.TryParse(sched.ScheduleTime, out var schedTime) && schedTime < createdAt.TimeOfDay)
                        continue;

                    var log = todayLogs.FirstOrDefault(l => l.ScheduleId == sched.Id);
                    allItems.Add(new TodayScheduleItem
                    {
                        MedicineId = med.Id,
                        ScheduleId = sched.Id,
                        MedicineName = med.Name,
                        StockUnit = med.StockUnit,
                        ScheduleTime = sched.ScheduleTime,
                        IsLogged = log != null,
                        WasTaken = log?.WasTaken == 1
                    });
                }
            }

            Items.Clear();
            foreach (var item in allItems.OrderBy(GetSortPriority).ThenBy(i => i.ScheduleTime))
                Items.Add(item);

            RecalcCounts();
        }
        catch (Exception ex) { SetError("Failed to load today's schedule."); System.Diagnostics.Debug.WriteLine(ex); }
        finally { IsBusy = false; }
    }

    public void RefreshWindows()
    {
        foreach (var item in Items)
            item.RefreshWindow();
    }

    private async Task TakeDose(TodayScheduleItem item)
    {
        if (item.IsLogged || IsBusy) return;
        IsBusy = true; ClearError();
        try
        {
            var medicine = await _medicineService.GetMedicineById(item.MedicineId);
            if (medicine != null && _medicineService.HasInsufficientStock(medicine))
            {
                SetError($"Insufficient stock for {item.MedicineName}.");
                return;
            }
            await _medicineService.LogDoseTaken(item.MedicineId, item.ScheduleId);
            await _reminderService.CancelDoseReminders(item.MedicineId, item.ScheduleId);
            item.WasTaken = true;
            item.IsLogged = true;
            RecalcCounts();
        }
        catch (Exception ex) { SetError($"Failed: {ex.Message}"); System.Diagnostics.Debug.WriteLine(ex); }
        finally { IsBusy = false; }
    }

private static int GetSortPriority(TodayScheduleItem item)
    {
        if (item.IsLogged) return item.WasTaken ? 4 : 5;
        if (!TimeSpan.TryParse(item.ScheduleTime, out var scheduled)) return 2;
        var now = DateTime.Now.TimeOfDay;
        if (now >= scheduled.Subtract(TimeSpan.FromHours(1)) && now <= scheduled.Add(TimeSpan.FromHours(2))) return 1;
        if (now > scheduled.Add(TimeSpan.FromHours(2))) return 3;
        return 2;
    }

    private void RecalcCounts()
    {
        TakenDoses = Items.Count(i => i.IsLogged && i.WasTaken);
        TotalDoses = Items.Count;
    }
}
