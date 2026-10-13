using HealthTracker.Views;

namespace HealthTracker;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute(nameof(MedicineDetailPage), typeof(MedicineDetailPage));
        Routing.RegisterRoute(nameof(AddMedicinePage), typeof(AddMedicinePage));
        Routing.RegisterRoute(nameof(AddMetricPage), typeof(AddMetricPage));
        Routing.RegisterRoute(nameof(MetricHistoryPage), typeof(MetricHistoryPage));
        Routing.RegisterRoute(nameof(AddReportPage), typeof(AddReportPage));
        Routing.RegisterRoute(nameof(AddDoctorVisitPage), typeof(AddDoctorVisitPage));
        Routing.RegisterRoute(nameof(DoctorVisitDetailPage), typeof(DoctorVisitDetailPage));
        Routing.RegisterRoute(nameof(EditDoctorVisitPage), typeof(EditDoctorVisitPage));
        Routing.RegisterRoute(nameof(EditMedicinePage), typeof(EditMedicinePage));
        Routing.RegisterRoute(nameof(MedicineListPage), typeof(MedicineListPage));
        Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));
        Routing.RegisterRoute(nameof(PinLockPage), typeof(PinLockPage));
        Routing.RegisterRoute("pinlock", typeof(PinLockPage));
        Routing.RegisterRoute(nameof(ScanPrescriptionPage), typeof(ScanPrescriptionPage));
        Routing.RegisterRoute(nameof(AskAiPage), typeof(AskAiPage));
    }

    private async void OnSettingsClicked(object sender, EventArgs e)
    {
        try
        {
            var page = App.Services.GetRequiredService<SettingsPage>();
            await Shell.Current.Navigation.PushAsync(page);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Settings nav failed: {ex}");
            await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private async void OnAskAiClicked(object sender, EventArgs e)
    {
        try
        {
            var page = App.Services.GetRequiredService<AskAiPage>();
            await Shell.Current.Navigation.PushAsync(page);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AskAI nav failed: {ex}");
            await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
        }
    }
}
