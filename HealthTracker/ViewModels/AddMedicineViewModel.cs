using System.Collections.ObjectModel;
using System.Windows.Input;
using HealthTracker.Helpers;
using HealthTracker.Services;

namespace HealthTracker.ViewModels;

public class ScheduleEntry : BaseViewModel
{
    private TimeSpan _time = new TimeSpan(8, 0, 0);
    public TimeSpan Time { get => _time; set { _time = value; OnPropertyChanged(); } }
}

public class AddMedicineViewModel : BaseViewModel
{
    private readonly MedicineService _service;

    private string _name = string.Empty;
    private string _prescribedDaysText = string.Empty;
    private string _stockText = string.Empty;
    private string _stockUnit = "Tablet";
    private string? _instructions;
    private string? _notes;

    // These can be pre-filled from a scanned prescription (MedicineListPage.OnPendingTapped) after
    // the page's BindingContext is set, so they must raise PropertyChanged.
    public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }
    public string PrescribedDaysText { get => _prescribedDaysText; set { _prescribedDaysText = value; OnPropertyChanged(); } }

    private bool _isOngoing;
    public bool IsOngoing
    {
        get => _isOngoing;
        set { _isOngoing = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsPrescribedDaysVisible)); }
    }
    public bool IsPrescribedDaysVisible => !IsOngoing;

    public string StockText { get => _stockText; set { _stockText = value; OnPropertyChanged(); } }
    public string StockUnit { get => _stockUnit; set { _stockUnit = value; OnPropertyChanged(); } }
    public string[] StockUnitOptions { get; } = ["Tablet", "Capsule", "Drop", "Syrup", "Injection", "Spray", "Inhaler", "Other"];
    public DateTime StartDate { get; set; } = DateTime.Today;
    public TimeSpan StartTime { get; set; } = DateTime.Now.TimeOfDay;
    public DateTime? EndDate { get; set; }
    public string? Instructions { get => _instructions; set { _instructions = value; OnPropertyChanged(); } }
    public string? Notes { get => _notes; set { _notes = value; OnPropertyChanged(); } }

    public ObservableCollection<ScheduleEntry> Schedules { get; } = new();
    public ICommand AddScheduleCommand { get; }
    public ICommand RemoveScheduleCommand { get; }
    public ICommand SaveCommand { get; }
    public event EventHandler? SavedSuccessfully;

    public AddMedicineViewModel(MedicineService service)
    {
        _service = service;
        Schedules.Add(new ScheduleEntry());
        AddScheduleCommand = new Command(() => Schedules.Add(new ScheduleEntry()));
        RemoveScheduleCommand = new Command<ScheduleEntry>(s => { if (Schedules.Count > 1) Schedules.Remove(s); });
        SaveCommand = new Command(async () => await Save());
    }

    private async Task Save()
    {
        if (IsBusy) return;
        IsBusy = true;
        ClearError();
        try
        {
            if (string.IsNullOrWhiteSpace(Name)) { SetError("Medicine name is required."); return; }
            int pd = 0;
            if (!IsOngoing && (!int.TryParse(PrescribedDaysText, out pd) || pd <= 0)) { SetError("Prescribed days is required."); return; }
            if (!ValidationHelper.TryParseDouble(StockText, out var stock) || stock < 0) { SetError("Enter a valid stock count."); return; }
            if (Schedules.Count == 0) { SetError("At least one schedule time is required."); return; }
            var schedList = Schedules.Select(s => s.Time.ToString(@"hh\:mm")).Distinct().ToList();
            var endDateStr = EndDate.HasValue ? DateTimeHelper.ToDateString(EndDate.Value) : null;
            var startDateTime = DateTimeHelper.ToDateTimeString(StartDate.Date.Add(StartTime));
            var medicine = await _service.AddMedicine(Name, stock, StockUnit,
                startDateTime, endDateStr, Instructions, Notes, pd, IsOngoing, schedList);

            if (StartDate.Date < DateTime.Today)
            {
                var pastDays = (DateTime.Today - StartDate.Date).Days;
                var backfill = await Shell.Current.DisplayAlert(
                    "Log Past Doses?",
                    $"You started this medicine {pastDays} day(s) ago. Mark all past doses as taken?",
                    "Yes", "No");

                if (backfill)
                {
                    var count = await _service.BackfillPastDoses(medicine.Id, StartDate.Date, DateTime.Today.AddDays(-1));
                    await Shell.Current.DisplayAlert("Done", $"{count} past dose(s) logged as taken.", "OK");
                }
            }

            SavedSuccessfully?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex) { SetError(ex.Message); System.Diagnostics.Debug.WriteLine(ex); }
        finally { IsBusy = false; }
    }
}
