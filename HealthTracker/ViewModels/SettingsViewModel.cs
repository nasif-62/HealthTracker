using System.Windows.Input;
using HealthTracker.Helpers;
using HealthTracker.Services;

namespace HealthTracker.ViewModels;

public class SettingsViewModel : BaseViewModel
{
    private readonly BackupService _backupService;
    private readonly DatabaseService _databaseService;
    private readonly HealthReportService _reportService;
    private readonly IReminderService _reminderService;

    private bool _isGeneratingReport;
    public bool IsGeneratingReport
    {
        get => _isGeneratingReport;
        set { _isGeneratingReport = value; OnPropertyChanged(); }
    }

    public string NewPin { get; set; } = string.Empty;
    public string ConfirmPin { get; set; } = string.Empty;
    public bool PinConfigured { get; private set; }

    private string _geminiApiKey = string.Empty;
    public string GeminiApiKey
    {
        get => _geminiApiKey;
        set { _geminiApiKey = value; OnPropertyChanged(); }
    }

    private bool _aiConfigured;
    public bool AiConfigured
    {
        get => _aiConfigured;
        set { _aiConfigured = value; OnPropertyChanged(); OnPropertyChanged(nameof(AiNotConfigured)); }
    }
    public bool AiNotConfigured => !AiConfigured;

    private string _aiStatusMessage = string.Empty;
    public string AiStatusMessage
    {
        get => _aiStatusMessage;
        set { _aiStatusMessage = value; OnPropertyChanged(); }
    }

    public ICommand SavePinCommand { get; }
    public ICommand RemovePinCommand { get; }
    public ICommand BackupCommand { get; }
    public ICommand DeleteAllDataCommand { get; }
    public ICommand SaveApiKeyCommand { get; }
    public ICommand RemoveApiKeyCommand { get; }
    public ICommand GenerateReportCommand { get; }
    public ICommand GenerateReportWithFilesCommand { get; }

    public SettingsViewModel(BackupService backupService, DatabaseService databaseService,
        HealthReportService reportService, IReminderService reminderService)
    {
        _backupService = backupService;
        _databaseService = databaseService;
        _reportService = reportService;
        _reminderService = reminderService;
        SavePinCommand = new Command(async () => await SavePin());
        RemovePinCommand = new Command(async () => await RemovePin());
        BackupCommand = new Command(async () => await Backup());
        DeleteAllDataCommand = new Command(async () => await DeleteAllData());
        SaveApiKeyCommand = new Command(SaveApiKey);
        RemoveApiKeyCommand = new Command(RemoveApiKey);
        GenerateReportCommand = new Command(async () => await GenerateReport(false));
        GenerateReportWithFilesCommand = new Command(async () => await GenerateReport(true));
        _ = LoadPinStatus();
        LoadApiKeyStatus();
    }

    private void LoadApiKeyStatus()
    {
        AiConfigured = AiParserService.IsConfigured;
        if (AiConfigured)
        {
            var key = AiParserService.GetApiKey() ?? "";
            GeminiApiKey = key.Length > 8 ? key[..4] + "****" + key[^4..] : "****";
            AiStatusMessage = "AI scanning is active.";
        }
    }

    private void SaveApiKey()
    {
        ClearError();
        AiStatusMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(GeminiApiKey) || GeminiApiKey.Contains("****"))
        {
            SetError("Enter your full Gemini API key.");
            return;
        }
        AiParserService.SetApiKey(GeminiApiKey.Trim());
        AiConfigured = true;
        var key = GeminiApiKey.Trim();
        GeminiApiKey = key.Length > 8 ? key[..4] + "****" + key[^4..] : "****";
        AiStatusMessage = "API key saved. AI scanning is now active.";
    }

    private void RemoveApiKey()
    {
        AiParserService.SetApiKey(null);
        GeminiApiKey = string.Empty;
        AiConfigured = false;
        AiStatusMessage = string.Empty;
    }

    private async Task LoadPinStatus()
    {
        try
        {
            var pin = await SecureStorage.GetAsync("app_pin");
            PinConfigured = !string.IsNullOrEmpty(pin);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"PIN status read failed: {ex}");
            PinConfigured = false;
        }
        OnPropertyChanged(nameof(PinConfigured));
    }

    private async Task SavePin()
    {
        ClearError();
        if (!ValidationHelper.IsValidPin(NewPin, out var err)) { SetError(err); return; }
        if (NewPin != ConfirmPin) { SetError("PINs do not match."); return; }
        IsBusy = true;
        try
        {
            await SecureStorage.SetAsync("app_pin", NewPin);
            PinConfigured = true;
            OnPropertyChanged(nameof(PinConfigured));
            NewPin = string.Empty; ConfirmPin = string.Empty;
            OnPropertyChanged(nameof(NewPin)); OnPropertyChanged(nameof(ConfirmPin));
        }
        catch (Exception ex) { SetError("Failed to save PIN."); System.Diagnostics.Debug.WriteLine(ex); }
        finally { IsBusy = false; }
    }

    private async Task RemovePin()
    {
        try
        {
            SecureStorage.Remove("app_pin");
            PinConfigured = false;
            OnPropertyChanged(nameof(PinConfigured));
        }
        catch (Exception ex) { SetError("Failed to remove PIN."); System.Diagnostics.Debug.WriteLine(ex); }
        await Task.CompletedTask;
    }

    private async Task Backup()
    {
        IsBusy = true; ClearError();
        try { await _backupService.CreateBackup(); }
        catch (Exception ex) { SetError("Backup failed. " + ex.Message); System.Diagnostics.Debug.WriteLine(ex); }
        finally { IsBusy = false; }
    }

    private async Task GenerateReport(bool includeFiles)
    {
        IsGeneratingReport = true; ClearError();
        try
        {
            var (filePath, warning) = await _reportService.GenerateAsync(includeFiles);

            if (!string.IsNullOrEmpty(warning))
                await Shell.Current.DisplayAlert("Large File", warning, "OK");

            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Health Report",
                File = new ShareFile(filePath, "application/pdf")
            });
        }
        catch (Exception ex)
        {
            SetError("Failed to generate report. " + ex.Message);
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally { IsGeneratingReport = false; }
    }

    private async Task DeleteAllData()
    {
        var first = await Shell.Current.DisplayAlert(
            "Delete All Data",
            "This will permanently delete ALL medicines, metrics, reports, doctor visits, and logs. This cannot be undone.",
            "Delete Everything", "Cancel");
        if (!first) return;

        var second = await Shell.Current.DisplayAlert(
            "Are you sure?",
            "All your health data will be lost forever. Consider creating a backup first.",
            "Yes, Delete All", "Cancel");
        if (!second) return;

        IsBusy = true; ClearError();
        try
        {
            // Reminders for deleted medicines must not keep firing until the next cold start.
            await _reminderService.CancelAll();
            await _databaseService.DeleteAllDataAsync();

            // Remove stored report/prescription files too, otherwise they linger and get backed up.
            var baseFolder = FileStorageHelper.GetBaseFolder();
            if (Directory.Exists(baseFolder))
            {
                try { Directory.Delete(baseFolder, recursive: true); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"File cleanup failed: {ex}"); }
            }

            await Shell.Current.DisplayAlert("Done", "All data has been deleted.", "OK");
        }
        catch (Exception ex) { SetError("Failed to delete data. " + ex.Message); System.Diagnostics.Debug.WriteLine(ex); }
        finally { IsBusy = false; }
    }
}
