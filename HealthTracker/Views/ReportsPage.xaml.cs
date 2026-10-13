using HealthTracker.Models;
using HealthTracker.ViewModels;

namespace HealthTracker.Views;

public partial class ReportsPage : ContentPage
{
    private readonly ReportsViewModel _vm;

    public ReportsPage(ReportsViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.Load();
    }

    private async void OnAddClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(App.Services.GetService<AddReportPage>()!);
    }

    private async void OnViewClicked(object sender, EventArgs e)
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

    private async void OnShareClicked(object sender, EventArgs e)
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

    private async void OnDeleteClicked(object sender, EventArgs e)
    {
        if (((Button)sender).CommandParameter is MedicalReport report)
        {
            _vm.DeleteCommand.Execute(report);
        }
    }
}
