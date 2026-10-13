using HealthTracker.Models;
using HealthTracker.ViewModels;

namespace HealthTracker.Views;

public partial class DoctorVisitsPage : ContentPage
{
    private readonly DoctorVisitsViewModel _vm;

    public DoctorVisitsPage(DoctorVisitsViewModel vm)
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
        await Navigation.PushAsync(App.Services.GetService<AddDoctorVisitPage>()!);
    }

    private async void OnViewClicked(object sender, EventArgs e)
    {
        if (((Button)sender).CommandParameter is DoctorVisit visit)
        {
            var page = App.Services.GetService<DoctorVisitDetailPage>()!;
            await page.LoadVisit(visit.Id);
            await Navigation.PushAsync(page);
        }
    }

    private async void OnEditClicked(object sender, EventArgs e)
    {
        if (((Button)sender).CommandParameter is DoctorVisit visit)
        {
            var page = App.Services.GetService<EditDoctorVisitPage>()!;
            await page.LoadVisit(visit.Id);
            await Navigation.PushAsync(page);
        }
    }
}
