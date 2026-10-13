using HealthTracker.ViewModels;

namespace HealthTracker.Views;

public partial class TodayPage : ContentPage
{
    private readonly TodayViewModel _vm;
    private CancellationTokenSource? _timerCts;

    public TodayPage(TodayViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.Load();
        _vm.RefreshWindows();
        _timerCts = new CancellationTokenSource();
        _ = RunRefreshLoop(_timerCts.Token);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _timerCts?.Cancel();
        _timerCts = null;
    }

    private async Task RunRefreshLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(30000, ct); }
            catch (OperationCanceledException) { return; }
            if (!ct.IsCancellationRequested)
                MainThread.BeginInvokeOnMainThread(() => _vm.RefreshWindows());
        }
    }

    private async void OnItemTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is not TodayScheduleItem item) return;
        var page = App.Services.GetService<MedicineDetailPage>()!;
        page.SetMedicineId(item.MedicineId);
        await Navigation.PushAsync(page);
    }

    private async void OnAddMedicineClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(App.Services.GetService<AddMedicinePage>()!);
    }

    private async void OnManageMedicinesClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(App.Services.GetService<MedicineListPage>()!);
    }
}
