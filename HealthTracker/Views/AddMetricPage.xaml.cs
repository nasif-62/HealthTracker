using HealthTracker.ViewModels;

namespace HealthTracker.Views;

public partial class AddMetricPage : ContentPage
{
    public AddMetricPage(AddMetricViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
        vm.SavedSuccessfully += async (s, e) => await Navigation.PopAsync();
    }
}
