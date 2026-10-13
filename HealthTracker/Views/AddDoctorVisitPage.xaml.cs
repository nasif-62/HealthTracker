using HealthTracker.ViewModels;

namespace HealthTracker.Views;

public partial class AddDoctorVisitPage : ContentPage
{
    public AddDoctorVisitPage(AddDoctorVisitViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
        vm.SavedSuccessfully += async (s, e) => await Navigation.PopAsync();
    }
}
