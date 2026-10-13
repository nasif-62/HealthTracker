using HealthTracker.ViewModels;

namespace HealthTracker.Views;

public partial class SettingsPage : ContentPage
{
    public SettingsPage(SettingsViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    private async void OnAiStudioLinkTapped(object? sender, TappedEventArgs e)
    {
        await Browser.Default.OpenAsync("https://aistudio.google.com/apikey", BrowserLaunchMode.SystemPreferred);
    }
}
