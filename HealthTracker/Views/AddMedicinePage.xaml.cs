using HealthTracker.ViewModels;

namespace HealthTracker.Views;

public partial class AddMedicinePage : ContentPage
{
    public AddMedicinePage(AddMedicineViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
        vm.SavedSuccessfully += async (s, e) => await Navigation.PopAsync();
    }
}
