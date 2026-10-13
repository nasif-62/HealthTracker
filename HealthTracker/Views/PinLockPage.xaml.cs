using HealthTracker.ViewModels;

namespace HealthTracker.Views;

public partial class PinLockPage : ContentPage
{
    public PinLockPage(PinLockViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
        vm.UnlockSucceeded += async (s, e) =>
        {
            try
            {
                if (Navigation.ModalStack.Contains(this))
                    await Navigation.PopModalAsync();
                else if (Navigation.NavigationStack.Contains(this))
                    await Navigation.PopAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Unlock navigation failed: {ex}");
            }
        };
    }

    // The lock screen must not be dismissible with the hardware/gesture back button.
    protected override bool OnBackButtonPressed() => true;
}
