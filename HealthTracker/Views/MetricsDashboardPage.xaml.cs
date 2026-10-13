using HealthTracker.ViewModels;

namespace HealthTracker.Views;

public partial class MetricsDashboardPage : ContentPage
{
    private readonly MetricsDashboardViewModel _vm;

    public MetricsDashboardPage(MetricsDashboardViewModel vm)
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
        await Navigation.PushAsync(App.Services.GetService<AddMetricPage>()!);
    }

    private async void OnMetricTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is MetricSummaryItem item)
        {
            var page = App.Services.GetService<MetricHistoryPage>()!;
            await page.LoadType(item.Type);
            await Navigation.PushAsync(page);
        }
    }
}
