using System.Collections.ObjectModel;
using System.Windows.Input;
using HealthTracker.Models;
using HealthTracker.Services;

namespace HealthTracker.ViewModels;

public class ReportsViewModel : BaseViewModel
{
    private readonly ReportService _service;
    public ObservableCollection<MedicalReport> Reports { get; } = new();
    public ICommand LoadCommand { get; }
    public ICommand DeleteCommand { get; }

    public ReportsViewModel(ReportService service)
    {
        _service = service;
        LoadCommand = new Command(async () => await Load());
        DeleteCommand = new Command<MedicalReport>(async r => await Delete(r));
    }

    public async Task Load()
    {
        IsBusy = true; ClearError();
        try
        {
            var list = await _service.GetAllReports();
            Reports.Clear();
            foreach (var r in list) Reports.Add(r);
        }
        catch (Exception ex) { SetError("Failed to load reports."); System.Diagnostics.Debug.WriteLine(ex); }
        finally { IsBusy = false; }
    }

    private async Task Delete(MedicalReport report)
    {
        bool confirmed = await Shell.Current.DisplayAlert(
            "Delete Report",
            $"Delete \"{report.Title}\"? The file will also be removed.",
            "Delete", "Cancel");
        if (!confirmed) return;

        IsBusy = true; ClearError();
        try { await _service.DeleteReport(report.Id); Reports.Remove(report); }
        catch (Exception ex) { SetError("Failed to delete report."); System.Diagnostics.Debug.WriteLine(ex); }
        finally { IsBusy = false; }
    }
}
