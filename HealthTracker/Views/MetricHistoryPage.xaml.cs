using HealthTracker.Models;
using HealthTracker.ViewModels;

namespace HealthTracker.Views;

public partial class MetricHistoryPage : ContentPage
{
    private readonly MetricHistoryViewModel _vm;
    private bool _loadedOnce;

    public MetricHistoryPage(MetricHistoryViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    public async Task LoadType(string metricType)
    {
        _loadedOnce = true;
        await _vm.Load(metricType);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // Reload when returning from the edit page so the list, chart and stats reflect the change.
        if (_loadedOnce && !string.IsNullOrEmpty(_vm.MetricType))
            await _vm.Load(_vm.MetricType);
    }

    private async void OnRecordTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is not HealthMetric metric) return;
        var page = App.Services.GetService<EditMetricPage>()!;
        var editVm = (EditMetricViewModel)page.BindingContext;
        editVm.Load(metric);
        page.Title = $"Edit {metric.MetricType}";
        await Navigation.PushAsync(page);
    }
}
