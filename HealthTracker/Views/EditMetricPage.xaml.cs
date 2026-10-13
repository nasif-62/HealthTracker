using HealthTracker.ViewModels;

namespace HealthTracker.Views;

public partial class EditMetricPage : ContentPage
{
    private readonly EditMetricViewModel _vm;

    public EditMetricPage(EditMetricViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
        _vm.SavedSuccessfully += async (_, _) => await Navigation.PopAsync();
    }
}
