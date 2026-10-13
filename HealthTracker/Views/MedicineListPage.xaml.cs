using HealthTracker.Models;
using HealthTracker.ViewModels;

namespace HealthTracker.Views;

public partial class MedicineListPage : ContentPage
{
    private readonly MedicineListViewModel _vm;

    public MedicineListPage(MedicineListViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadMedicines();
    }

    private async void OnScanClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(App.Services.GetService<ScanPrescriptionPage>()!);
    }

    private async void OnAddClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(App.Services.GetService<AddMedicinePage>()!);
    }

    private async void OnMedicineTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is MedicineItem item)
        {
            var page = App.Services.GetService<MedicineDetailPage>()!;
            page.SetMedicineId(item.Medicine.Id);
            await Navigation.PushAsync(page);
        }
    }

    private async void OnPendingTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is not PendingMedicine pending) return;

        var page = App.Services.GetService<AddMedicinePage>()!;
        if (page.BindingContext is AddMedicineViewModel addVm)
        {
            addVm.Name = pending.Name;

            // Build instructions from parsed data
            var parts = new[] { pending.Form, pending.Dosage, pending.Frequency }
                .Where(s => !string.IsNullOrEmpty(s));
            addVm.Instructions = string.Join(" — ", parts);

            // Set stock unit from form
            if (!string.IsNullOrEmpty(pending.Form))
            {
                var lower = pending.Form.ToLowerInvariant();
                if (lower.Contains("capsule")) addVm.StockUnit = "Capsule";
                else if (lower.Contains("syrup")) addVm.StockUnit = "Syrup";
                else if (lower.Contains("injection")) addVm.StockUnit = "Injection";
                else if (lower.Contains("drop")) addVm.StockUnit = "Drop";
                else if (lower.Contains("inhaler")) addVm.StockUnit = "Inhaler";
                else if (lower.Contains("spray")) addVm.StockUnit = "Spray";
            }

            // Remove from pending when saved
            addVm.SavedSuccessfully += async (_, _) => await _vm.ApprovePending(pending);
        }

        await Navigation.PushAsync(page);
    }
}
