namespace HealthTracker.Constants;

public static class AppConstants
{
    public const string DatabaseFilename = "healthapp.db";
    public const string HealthAppFolder = "HealthApp";
    public const string ReportsFolder = "reports";
    public const string PrescriptionsFolder = "prescriptions";
    public const string ScansFolder = "scans";
    public const string OtherFolder = "other";
    public const int LowStockMultiplier = 5;
    public const int PinMinLength = 4;
    public const int PinMaxLength = 6;
    public const int MaxFailedPinAttempts = 5;
    public const int PinCooldownSeconds = 30;
    public const int BackgroundResumeThresholdMinutes = 2;
}
