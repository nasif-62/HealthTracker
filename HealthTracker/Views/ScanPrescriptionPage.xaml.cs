using HealthTracker.ViewModels;

namespace HealthTracker.Views;

public partial class ScanPrescriptionPage : ContentPage
{
    public ScanPrescriptionPage(ScanPrescriptionViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
