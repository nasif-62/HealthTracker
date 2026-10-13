using System.Globalization;
using HealthTracker.Helpers;

namespace HealthTracker.Services;

public class BackupService
{
    /// <summary>Older backup ZIPs beyond this count are removed from the cache before a new one is created.</summary>
    private const int KeepRecentBackups = 2;

    public async Task CreateBackup()
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm", CultureInfo.InvariantCulture);
        var cacheDir = Path.Combine(FileSystem.CacheDirectory, "Backups");
        Directory.CreateDirectory(cacheDir);
        PruneOldBackups(cacheDir);

        var tempFolder = Path.Combine(cacheDir, $"HealthBackup_{timestamp}");
        var zipPath = Path.Combine(cacheDir, $"HealthBackup_{timestamp}.zip");

        try
        {
            Directory.CreateDirectory(tempFolder);

            // Copy database
            var dbPath = FileStorageHelper.GetDatabasePath();
            if (File.Exists(dbPath))
                File.Copy(dbPath, Path.Combine(tempFolder, "healthapp.db"), true);

            // Copy files directory
            var baseFolder = FileStorageHelper.GetBaseFolder();
            if (Directory.Exists(baseFolder))
                CopyDirectory(baseFolder, Path.Combine(tempFolder, "files"));

            // Create ZIP
            if (File.Exists(zipPath)) File.Delete(zipPath);
            System.IO.Compression.ZipFile.CreateFromDirectory(tempFolder, zipPath);

            // Share — ZIP must exist until the receiver reads it
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = $"HealthTracker Backup {timestamp}",
                File = new ShareFile(zipPath)
            });
        }
        finally
        {
            // Only clean up the staging folder, not the ZIP (receiver may still need it)
            if (Directory.Exists(tempFolder))
                Directory.Delete(tempFolder, true);
        }
    }

    private static void PruneOldBackups(string cacheDir)
    {
        try
        {
            var zips = Directory.GetFiles(cacheDir, "HealthBackup_*.zip")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .Skip(KeepRecentBackups);
            foreach (var zip in zips)
                File.Delete(zip);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Backup prune failed: {ex.Message}");
        }
    }

    private static void CopyDirectory(string source, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), true);
        foreach (var dir in Directory.GetDirectories(source))
            CopyDirectory(dir, Path.Combine(dest, Path.GetFileName(dir)));
    }
}
