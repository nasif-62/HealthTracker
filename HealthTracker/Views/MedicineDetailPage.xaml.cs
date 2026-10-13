using HealthTracker.ViewModels;

namespace HealthTracker.Views;

public partial class MedicineDetailPage : ContentPage
{
    private readonly MedicineDetailViewModel _vm;
    private int _medicineId;

    public MedicineDetailPage(MedicineDetailViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    public void SetMedicineId(int medicineId)
    {
        _medicineId = medicineId;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_medicineId > 0)
            await _vm.Load(_medicineId);
    }

    private async void OnEditClicked(object sender, EventArgs e)
    {
        if (_medicineId <= 0) return;
        var page = App.Services.GetService<EditMedicinePage>()!;
        await page.LoadMedicine(_medicineId);
        await Navigation.PushAsync(page);
    }
}
