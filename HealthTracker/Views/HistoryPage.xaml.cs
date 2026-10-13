using HealthTracker.ViewModels;

namespace HealthTracker.Views;

public partial class HistoryPage : ContentPage
{
    private readonly HistoryViewModel _vm;

    public HistoryPage(HistoryViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    public void SetCategory(string category)
    {
        _vm.Category = category;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.Search();
    }

    private async void OnMedicineTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is not CourseSummaryRow row) return;
        var page = App.Services.GetService<MedicineDetailPage>()!;
        page.SetMedicineId(row.MedicineId);
        await Navigation.PushAsync(page);
    }

    private async void OnDoctorTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is not DoctorHistoryItem item) return;
        var page = App.Services.GetService<DoctorVisitDetailPage>()!;
        await page.LoadVisit(item.VisitId);
        await Navigation.PushAsync(page);
    }

    private async void OnReportTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is not ReportHistoryItem item) return;

        if (string.IsNullOrEmpty(item.FilePath) || !File.Exists(item.FilePath))
        {
            await DisplayAlert("Not Found", "File not found on device.", "OK");
            return;
        }

        await Launcher.Default.OpenAsync(new OpenFileRequest(item.Title, new ReadOnlyFile(item.FilePath)));
    }
}
