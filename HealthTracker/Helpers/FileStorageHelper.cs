using HealthTracker.Constants;

namespace HealthTracker.Helpers;

public static class FileStorageHelper
{
    public static string GetBaseFolder() =>
        Path.Combine(FileSystem.AppDataDirectory, AppConstants.HealthAppFolder);

    public static string GetSubFolder(string reportType) => reportType switch
    {
        "Lab" => Path.Combine(GetBaseFolder(), AppConstants.ReportsFolder),
        "Prescription" => Path.Combine(GetBaseFolder(), AppConstants.PrescriptionsFolder),
        "Xray" or "Scan" => Path.Combine(GetBaseFolder(), AppConstants.ScansFolder),
        _ => Path.Combine(GetBaseFolder(), AppConstants.OtherFolder)
    };

    public static void EnsureFoldersExist()
    {
        Directory.CreateDirectory(GetBaseFolder());
        Directory.CreateDirectory(Path.Combine(GetBaseFolder(), AppConstants.ReportsFolder));
        Directory.CreateDirectory(Path.Combine(GetBaseFolder(), AppConstants.PrescriptionsFolder));
        Directory.CreateDirectory(Path.Combine(GetBaseFolder(), AppConstants.ScansFolder));
        Directory.CreateDirectory(Path.Combine(GetBaseFolder(), AppConstants.OtherFolder));
    }

    public static string BuildFileName(string reportType, string reportDate, string ext)
    {
        var guid = Guid.NewGuid().ToString("N")[..8];
        return $"{reportType}_{reportDate}_{guid}.{ext.TrimStart('.')}";
    }

    public static string GetDatabasePath() =>
        Path.Combine(FileSystem.AppDataDirectory, AppConstants.DatabaseFilename);
}
