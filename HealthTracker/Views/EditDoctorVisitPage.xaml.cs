using HealthTracker.ViewModels;

namespace HealthTracker.Views;

public partial class EditDoctorVisitPage : ContentPage
{
    private readonly EditDoctorVisitViewModel _vm;

    public EditDoctorVisitPage(EditDoctorVisitViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
        vm.SavedSuccessfully += async (s, e) => await Navigation.PopAsync();
    }

    public async Task LoadVisit(int visitId)
    {
        await _vm.Load(visitId);
    }
}
