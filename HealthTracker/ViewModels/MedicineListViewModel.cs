using System.Collections.ObjectModel;
using System.Windows.Input;
using HealthTracker.Models;
using HealthTracker.Repositories;
using HealthTracker.Services;

namespace HealthTracker.ViewModels;

public class MedicineListViewModel : BaseViewModel
{
    private readonly MedicineService _service;
    private readonly PendingMedicineRepository _pendingRepo;
    public ObservableCollection<MedicineItem> Medicines { get; } = new();
    public ObservableCollection<MedicineItem> PastMedicines { get; } = new();
    public ObservableCollection<PendingMedicine> PendingMedicines { get; } = new();

    public bool HasPastMedicines => PastMedicines.Count > 0;
    public bool HasPendingMedicines => PendingMedicines.Count > 0;

    public ICommand LoadCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand DismissPendingCommand { get; }
    public ICommand DragStartedCommand { get; }
    public ICommand DropCommand { get; }

    private MedicineItem? _dragging;

    public MedicineListViewModel(MedicineService service, PendingMedicineRepository pendingRepo)
    {
        _service = service;
        _pendingRepo = pendingRepo;
        LoadCommand = new Command(async () => await LoadMedicines());
        DeleteCommand = new Command<MedicineItem>(async m => await Delete(m));
        DismissPendingCommand = new Command<PendingMedicine>(async p => await DismissPending(p));
        DragStartedCommand = new Command<MedicineItem>(item => _dragging = item);
        DropCommand = new Command<MedicineItem>(async target => await OnDrop(target));
    }

    public async Task LoadMedicines()
    {
        if (IsBusy) return;
        IsBusy = true; ClearError();
        try
        {
            await _service.CheckAndCompleteCourses();

            // Active medicines — sorted by SortOrder descending (highest = first; new medicines get highest)
            var list = (await _service.GetAllActiveMedicines())
                .OrderByDescending(m => m.SortOrder)
                .ThenByDescending(m => m.Id)
                .ToList();
            Medicines.Clear();
            foreach (var m in list)
            {
                var schedules = await _service.GetSchedulesForMedicine(m.Id);
                var todayLogs = await _service.GetLogsForMedicine(m.Id, DateTime.Today, DateTime.Today.AddDays(1).AddSeconds(-1));

                // Skip schedule slots that passed before the medicine was added today
                DateTime createdAt = default;
                var createdToday = DateTime.TryParse(m.CreatedAt, out createdAt) && createdAt.Date == DateTime.Today;
                var validSchedules = createdToday
                    ? schedules.Where(s => !TimeSpan.TryParse(s.ScheduleTime, out var t) || t >= createdAt.TimeOfDay).ToList()
                    : schedules;

                var taken = todayLogs.Count(l => l.WasTaken == 1);
                var total = validSchedules.Count;
                var progress = total > 0 ? Math.Min(1.0, (double)taken / total) : 0.0;
                var (streak, missedThisWeek) = await _service.GetAdherenceStats(m.Id);
                var stockDays = _service.GetStockDaysRemaining(m, schedules.Count);
                Medicines.Add(new MedicineItem
                {
                    Medicine = m,
                    DoseProgress = progress,
                    DoseProgressText = total > 0 ? $"{taken} of {total} doses taken today" : "No schedule set",
                    StockDaysRemaining = stockDays,
                    Streak = streak,
                    MissedThisWeek = missedThisWeek
                });
            }

            // Past medicines
            var past = await _service.GetAllInactiveMedicines();
            PastMedicines.Clear();
            foreach (var m in past)
            {
                var courses = await _service.GetCoursesForMedicine(m.Id);
                PastMedicines.Add(new MedicineItem
                {
                    Medicine = m,
                    DoseProgress = 0,
                    DoseProgressText = $"{courses.Count} course(s) completed",
                    StockDaysRemaining = double.MaxValue,
                    Streak = 0,
                    MissedThisWeek = 0
                });
            }

            OnPropertyChanged(nameof(HasPastMedicines));

            // Pending medicines from OCR scan
            var pending = await _pendingRepo.GetAllAsync();
            PendingMedicines.Clear();
            foreach (var p in pending)
                PendingMedicines.Add(p);
            OnPropertyChanged(nameof(HasPendingMedicines));
        }
        catch (Exception ex) { SetError("Failed to load medicines."); System.Diagnostics.Debug.WriteLine(ex); }
        finally { IsBusy = false; }
    }

    private async Task Delete(MedicineItem item)
    {
        // Runs inside an async-void Command: every awaited call must be guarded or a DB error crashes the app.
        try
        {
            var courses = await _service.GetCoursesForMedicine(item.Medicine.Id);
            var hasHistory = courses.Any(c => c.Status == "Completed") || courses.Sum(c => c.TotalTaken) > 0;

            var message = hasHistory
                ? $"\"{item.Medicine.Name}\" has past course history with {courses.Sum(c => c.TotalTaken)} doses recorded. Deleting will permanently erase all history. This cannot be undone."
                : $"Delete \"{item.Medicine.Name}\"? All schedules will be removed. This cannot be undone.";

            bool confirmed = await Shell.Current.DisplayAlert("Delete Medicine", message, "Delete", "Cancel");
            if (!confirmed) return;

            IsBusy = true; ClearError();
            await _service.DeleteMedicine(item.Medicine.Id);
            Medicines.Remove(item);
            PastMedicines.Remove(item);
            OnPropertyChanged(nameof(HasPastMedicines));
        }
        catch (Exception ex) { SetError("Failed to delete medicine."); System.Diagnostics.Debug.WriteLine(ex); }
        finally { IsBusy = false; }
    }

    private async Task DismissPending(PendingMedicine item)
    {
        try
        {
            var confirmed = await Shell.Current.DisplayAlert("Dismiss", $"Remove \"{item.Name}\" from pending list?", "Dismiss", "Cancel");
            if (!confirmed) return;
            await _pendingRepo.DeleteAsync(item);
            PendingMedicines.Remove(item);
            OnPropertyChanged(nameof(HasPendingMedicines));
        }
        catch (Exception ex) { SetError("Failed to dismiss pending medicine."); System.Diagnostics.Debug.WriteLine(ex); }
    }

    public async Task ApprovePending(PendingMedicine item)
    {
        try
        {
            await _pendingRepo.DeleteAsync(item);
            PendingMedicines.Remove(item);
            OnPropertyChanged(nameof(HasPendingMedicines));
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"ApprovePending failed: {ex}"); }
    }

    private async Task OnDrop(MedicineItem target)
    {
        try
        {
            if (_dragging is null || _dragging == target) return;

            var from = Medicines.IndexOf(_dragging);
            var to = Medicines.IndexOf(target);
            if (from < 0 || to < 0) return;

            Medicines.Move(from, to);

            // Reassign SortOrder: index 0 = highest value so sort DESC keeps this order
            var count = Medicines.Count;
            for (int i = 0; i < count; i++)
                Medicines[i].Medicine.SortOrder = count - i;

            // Persist all
            foreach (var item in Medicines)
                await _service.UpdateSortOrder(item.Medicine.Id, item.Medicine.SortOrder);
        }
        catch (Exception ex) { SetError("Failed to save the new order."); System.Diagnostics.Debug.WriteLine(ex); }
        finally { _dragging = null; }
    }
}
