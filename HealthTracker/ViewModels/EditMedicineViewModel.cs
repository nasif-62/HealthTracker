using System.Collections.ObjectModel;
using System.Windows.Input;
using HealthTracker.Helpers;
using HealthTracker.Models;
using HealthTracker.Services;

namespace HealthTracker.ViewModels;

public class EditMedicineViewModel : BaseViewModel
{
    private readonly MedicineService _service;
    private Medicine? _medicine;

    public string Name { get; set; } = string.Empty;
    public string PrescribedDaysText { get; set; } = string.Empty;

    private bool _isOngoing;
    public bool IsOngoing
    {
        get => _isOngoing;
        set { _isOngoing = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsPrescribedDaysVisible)); }
    }
    public bool IsPrescribedDaysVisible => !IsOngoing;

    public string StockText { get; set; } = string.Empty;
    public string StockUnit { get; set; } = "Tablet";
    public string[] StockUnitOptions { get; } = ["Tablet", "Capsule", "Drop", "Syrup", "Injection", "Spray", "Inhaler", "Other"];
    public DateTime StartDate { get; set; } = DateTime.Today;
    public TimeSpan StartTime { get; set; } = TimeSpan.Zero;
    public string? Instructions { get; set; }
    public string? Notes { get; set; }

    public ObservableCollection<ScheduleEntry> Schedules { get; } = new();
    public ICommand AddScheduleCommand { get; }
    public ICommand RemoveScheduleCommand { get; }
    public ICommand SaveCommand { get; }
    public event EventHandler? SavedSuccessfully;

    public EditMedicineViewModel(MedicineService service)
    {
        _service = service;
        AddScheduleCommand = new Command(() => Schedules.Add(new ScheduleEntry()));
        RemoveScheduleCommand = new Command<ScheduleEntry>(s => { if (Schedules.Count > 1) Schedules.Remove(s); });
        SaveCommand = new Command(async () => await Save());
    }

    public async Task Load(int medicineId)
    {
        IsBusy = true; ClearError();
        try
        {
            _medicine = await _service.GetMedicineById(medicineId);
            if (_medicine is null) { SetError("Medicine not found."); return; }

            Name = _medicine.Name;
            StockText = _medicine.StockCount.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            StockUnit = _medicine.StockUnit;
            var parsedStart = DateTimeHelper.TryParse(_medicine.StartDate, out var d) ? d : DateTime.Today;
            StartDate = parsedStart.Date;
            StartTime = parsedStart.TimeOfDay;
            Instructions = _medicine.Instructions;
            Notes = _medicine.Notes;
            IsOngoing = _medicine.IsOngoing == 1;
            PrescribedDaysText = _medicine.PrescribedDays > 0 ? _medicine.PrescribedDays.ToString() : string.Empty;

            Schedules.Clear();
            var schedules = await _service.GetSchedulesForMedicine(medicineId);
            foreach (var s in schedules)
                Schedules.Add(new ScheduleEntry { Time = TimeSpan.TryParse(s.ScheduleTime, out var t) ? t : TimeSpan.FromHours(8) });
            if (Schedules.Count == 0) Schedules.Add(new ScheduleEntry());

            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(StockText));
            OnPropertyChanged(nameof(StockUnit));
            OnPropertyChanged(nameof(StartDate));
            OnPropertyChanged(nameof(StartTime));
            OnPropertyChanged(nameof(Instructions));
            OnPropertyChanged(nameof(Notes));
            OnPropertyChanged(nameof(IsOngoing));
            OnPropertyChanged(nameof(IsPrescribedDaysVisible));
            OnPropertyChanged(nameof(PrescribedDaysText));
        }
        catch (Exception ex) { SetError("Failed to load medicine."); System.Diagnostics.Debug.WriteLine(ex); }
        finally { IsBusy = false; }
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
            if (_medicine is null) return;

            _medicine.Name = Name;
            _medicine.StockCount = stock;
            _medicine.StockUnit = StockUnit;
            _medicine.StartDate = DateTimeHelper.ToDateTimeString(StartDate.Date.Add(StartTime));
            _medicine.Instructions = Instructions;
            _medicine.Notes = Notes;
            _medicine.PrescribedDays = pd;
            _medicine.IsOngoing = IsOngoing ? 1 : 0;

            var schedList = Schedules.Select(s => s.Time.ToString(@"hh\:mm")).Distinct().ToList();
            await _service.UpdateMedicine(_medicine, schedList);
            SavedSuccessfully?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex) { SetError(ex.Message); System.Diagnostics.Debug.WriteLine(ex); }
        finally { IsBusy = false; }
    }
}
