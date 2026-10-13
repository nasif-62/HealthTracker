using System.Collections.ObjectModel;
using System.Windows.Input;
using HealthTracker.Models;
using HealthTracker.Services;

namespace HealthTracker.ViewModels;

public class DoctorVisitsViewModel : BaseViewModel
{
    private readonly DoctorService _service;
    public ObservableCollection<DoctorVisit> Visits { get; } = new();
    public ICommand DeleteCommand { get; }

    public DoctorVisitsViewModel(DoctorService service)
    {
        _service = service;
        DeleteCommand = new Command<DoctorVisit>(async v => await Delete(v));
    }

    public async Task Load()
    {
        IsBusy = true; ClearError();
        try
        {
            var list = await _service.GetAllVisits();
            Visits.Clear();
            foreach (var v in list) Visits.Add(v);
        }
        catch (Exception ex) { SetError("Failed to load visits."); System.Diagnostics.Debug.WriteLine(ex); }
        finally { IsBusy = false; }
    }

    private async Task Delete(DoctorVisit visit)
    {
        bool confirmed = await Shell.Current.DisplayAlert(
            "Delete Visit",
            $"Delete visit with {visit.DoctorName}? This cannot be undone.",
            "Delete", "Cancel");
        if (!confirmed) return;

        IsBusy = true; ClearError();
        try
        {
            await _service.DeleteVisit(visit.Id);
            Visits.Remove(visit);
        }
        catch (Exception ex) { SetError("Failed to delete visit."); System.Diagnostics.Debug.WriteLine(ex); }
        finally { IsBusy = false; }
    }
}
