using HealthTracker.ViewModels;

namespace HealthTracker.Views;

public partial class EditMedicinePage : ContentPage
{
    private readonly EditMedicineViewModel _vm;

    public EditMedicinePage(EditMedicineViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
        vm.SavedSuccessfully += async (s, e) => await Navigation.PopAsync();
    }

    public async Task LoadMedicine(int medicineId)
    {
        await _vm.Load(medicineId);
    }
}
