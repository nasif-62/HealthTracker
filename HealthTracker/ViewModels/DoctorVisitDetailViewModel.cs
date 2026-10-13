using System.Collections.ObjectModel;
using HealthTracker.Models;
using HealthTracker.Services;

namespace HealthTracker.ViewModels;

public class DoctorVisitDetailViewModel : BaseViewModel
{
    private readonly DoctorService _doctorService;
    private readonly ReportService _reportService;
    private DoctorVisit? _visit;

    public DoctorVisit? Visit { get => _visit; set { _visit = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasPrescriptionImage)); } }
    public bool HasPrescriptionImage => !string.IsNullOrEmpty(Visit?.PrescriptionImagePath) && File.Exists(Visit.PrescriptionImagePath);
    public ObservableCollection<MedicalReport> Reports { get; } = new();

    public DoctorVisitDetailViewModel(DoctorService doctorService, ReportService reportService)
    {
        _doctorService = doctorService;
        _reportService = reportService;
    }

    public async Task Load(int visitId)
    {
        IsBusy = true; ClearError();
        try
        {
            Visit = await _doctorService.GetVisitById(visitId);
            var reports = await _reportService.GetReportsByVisit(visitId);
            Reports.Clear();
            foreach (var r in reports) Reports.Add(r);
        }
        catch (Exception ex) { SetError("Failed to load visit details."); System.Diagnostics.Debug.WriteLine(ex); }
        finally { IsBusy = false; }
    }
}
