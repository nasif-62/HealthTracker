using HealthTracker.Constants;
using HealthTracker.Services;
using HealthTracker.Views;

namespace HealthTracker;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;
    private DateTime? _backgroundedAt;

    public App(IServiceProvider services)
    {
        InitializeComponent();
        Services = services;
        UserAppTheme = AppTheme.Light;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }

    protected override async void OnStart()
    {
        base.OnStart();

        // Everything here runs inside an async void; an unhandled exception would kill the app at launch.
        try
        {
            // Request notification permission (Android 13+)
            await Plugin.LocalNotification.LocalNotificationCenter.Current.RequestNotificationPermission();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Notification permission request failed: {ex}");
        }

        try
        {
            // On Android 12+, check that exact alarm permission is granted
            await CheckExactAlarmPermission();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Exact alarm check failed: {ex}");
        }

        // Reschedule all reminders on launch (RULE-10)
        try
        {
            var reminderService = Services.GetRequiredService<IReminderService>();
            await reminderService.RescheduleAll();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RescheduleAll failed: {ex}");
        }

        await CheckPinAndLock();
    }

    private static async Task CheckExactAlarmPermission()
    {
#if ANDROID
        if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.S)
        {
            var alarmManager = Android.App.Application.Context
                .GetSystemService(Android.Content.Context.AlarmService) as Android.App.AlarmManager;
            if (alarmManager != null && !alarmManager.CanScheduleExactAlarms())
            {
                bool goToSettings = await Shell.Current.DisplayAlert(
                    "Enable Reminders",
                    "To receive medicine reminders on time, please allow 'Alarms & Reminders' for HealthTracker.",
                    "Open Settings", "Later");
                if (goToSettings)
                {
                    var intent = new Android.Content.Intent(
                        Android.Provider.Settings.ActionRequestScheduleExactAlarm,
                        Android.Net.Uri.FromParts("package", AppInfo.PackageName, null));
                    intent.AddFlags(Android.Content.ActivityFlags.NewTask);
                    Android.App.Application.Context.StartActivity(intent);
                }
            }
        }
#endif
        await Task.CompletedTask;
    }

    protected override void OnSleep()
    {
        base.OnSleep();
        _backgroundedAt = DateTime.Now;
    }

    protected override async void OnResume()
    {
        base.OnResume();
        if (_backgroundedAt.HasValue &&
            (DateTime.Now - _backgroundedAt.Value).TotalMinutes >= AppConstants.BackgroundResumeThresholdMinutes)
        {
            await CheckPinAndLock();
        }
    }

    private static async Task CheckPinAndLock()
    {
        try
        {
            var pin = await SecureStorage.GetAsync("app_pin");
            if (string.IsNullOrEmpty(pin)) return;

            var navigation = Shell.Current?.Navigation;
            if (navigation is null) return;

            // Don't stack a second lock screen on top of one that is already showing.
            if (navigation.ModalStack.Any(p => p is PinLockPage)) return;

            // Modal: hides the tab bar and the nav-bar back arrow; PinLockPage swallows the back button.
            var lockPage = Services.GetRequiredService<PinLockPage>();
            await navigation.PushModalAsync(lockPage, animated: false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"PIN check failed: {ex}");
        }
    }
}
