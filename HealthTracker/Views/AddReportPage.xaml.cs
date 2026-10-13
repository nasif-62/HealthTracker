using HealthTracker.ViewModels;

namespace HealthTracker.Views;

public partial class AddReportPage : ContentPage
{
    public AddReportPage(AddReportViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
        vm.SavedSuccessfully += async (s, e) => await Navigation.PopAsync();
    }
}
