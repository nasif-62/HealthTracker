using HealthTracker.Models;
using HealthTracker.ViewModels;

namespace HealthTracker.Views;

public partial class DoctorVisitDetailPage : ContentPage
{
    private readonly DoctorVisitDetailViewModel _vm;

    public DoctorVisitDetailPage(DoctorVisitDetailViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    public async Task LoadVisit(int visitId)
    {
        await _vm.Load(visitId);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_vm.Visit is not null)
            await _vm.Load(_vm.Visit.Id);
    }

    private async void OnUploadClicked(object sender, EventArgs e)
    {
        if (_vm.Visit is null) return;
        var addReportPage = App.Services.GetService<AddReportPage>()!;
        var addVm = (AddReportViewModel)addReportPage.BindingContext;
        addVm.DoctorVisitId = _vm.Visit.Id;
        addVm.DoctorName = _vm.Visit.DoctorName;
        addVm.Hospital = _vm.Visit.Hospital;
        addVm.ReportType = "Prescription";
        await Navigation.PushAsync(addReportPage);
    }

    private async void OnViewPrescriptionClicked(object sender, EventArgs e)
    {
        var path = _vm.Visit?.PrescriptionImagePath;
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            await DisplayAlert("Not Found", "Prescription image not found.", "OK");
            return;
        }
        await Launcher.Default.OpenAsync(new OpenFileRequest("Prescription", new ReadOnlyFile(path)));
    }

    private async void OnViewReportClicked(object sender, EventArgs e)
    {
        if (((Button)sender).CommandParameter is MedicalReport report)
        {
            if (!File.Exists(report.FilePath))
            {
                await DisplayAlert("Not Found", "File not found on device.", "OK");
                return;
            }
            await Launcher.Default.OpenAsync(new OpenFileRequest(report.Title, new ReadOnlyFile(report.FilePath)));
        }
    }

    private async void OnShareReportClicked(object sender, EventArgs e)
    {
        if (((Button)sender).CommandParameter is MedicalReport report)
        {
            if (!File.Exists(report.FilePath))
            {
                await DisplayAlert("Not Found", "File not found on device.", "OK");
                return;
            }
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = report.Title,
                File = new ShareFile(report.FilePath)
            });
        }
    }
}
