using System.Windows.Input;
using HealthTracker.Helpers;
using HealthTracker.Models;
using HealthTracker.Services;

namespace HealthTracker.ViewModels;

public class EditDoctorVisitViewModel : BaseViewModel
{
    private readonly DoctorService _service;
    private DoctorVisit? _visit;

    public string DoctorName { get; set; } = string.Empty;
    public string? Hospital { get; set; }
    public DateTime VisitDate { get; set; } = DateTime.Today;
    public string? Diagnosis { get; set; }
    public string? Notes { get; set; }
    public DateTime FollowUpDate { get; set; } = DateTime.Today;
    public bool HasFollowUp { get; set; }

    public ICommand SaveCommand { get; }
    public event EventHandler? SavedSuccessfully;

    public EditDoctorVisitViewModel(DoctorService service)
    {
        _service = service;
        SaveCommand = new Command(async () => await Save());
    }

    public async Task Load(int visitId)
    {
        IsBusy = true; ClearError();
        try
        {
            _visit = await _service.GetVisitById(visitId);
            if (_visit is null) { SetError("Visit not found."); return; }

            DoctorName = _visit.DoctorName;
            Hospital = _visit.Hospital;
            VisitDate = DateTimeHelper.TryParse(_visit.VisitDate, out var vd) ? vd : DateTime.Today;
            Diagnosis = _visit.Diagnosis;
            Notes = _visit.Notes;
            HasFollowUp = _visit.FollowUpDate is not null;
            FollowUpDate = DateTimeHelper.TryParse(_visit.FollowUpDate, out var fu) ? fu : DateTime.Today;

            OnPropertyChanged(nameof(DoctorName));
            OnPropertyChanged(nameof(Hospital));
            OnPropertyChanged(nameof(VisitDate));
            OnPropertyChanged(nameof(Diagnosis));
            OnPropertyChanged(nameof(Notes));
            OnPropertyChanged(nameof(HasFollowUp));
            OnPropertyChanged(nameof(FollowUpDate));
        }
        catch (Exception ex) { SetError("Failed to load visit."); System.Diagnostics.Debug.WriteLine(ex); }
        finally { IsBusy = false; }
    }

    private async Task Save()
    {
        if (IsBusy) return;
        IsBusy = true;
        ClearError();
        try
        {
            if (string.IsNullOrWhiteSpace(DoctorName)) { SetError("Doctor name is required."); return; }
            if (_visit is null) return;

            // Only enforce the "not in the past" rule when the follow-up date is being changed,
            // so an old visit with an already-passed follow-up can still be edited.
            var newFollowUp = HasFollowUp ? DateTimeHelper.ToDateString(FollowUpDate) : null;
            if (newFollowUp is not null && newFollowUp != _visit.FollowUpDate && FollowUpDate.Date < DateTime.Today)
            {
                SetError("Follow-up date must be today or a future date.");
                return;
            }

            _visit.DoctorName = DoctorName;
            _visit.Hospital = Hospital;
            _visit.VisitDate = DateTimeHelper.ToDateString(VisitDate);
            _visit.Diagnosis = Diagnosis;
            _visit.Notes = Notes;
            _visit.FollowUpDate = newFollowUp;

            await _service.UpdateVisit(_visit);
            SavedSuccessfully?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex) { SetError(ex.Message); System.Diagnostics.Debug.WriteLine(ex); }
        finally { IsBusy = false; }
    }
}
