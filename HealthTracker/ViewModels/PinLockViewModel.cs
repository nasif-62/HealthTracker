using System.Windows.Input;
using HealthTracker.Constants;

namespace HealthTracker.ViewModels;

public class PinLockViewModel : BaseViewModel
{
    private int _failedAttempts = 0;
    private string _enteredPin = string.Empty;
    private bool _isCoolingDown = false;

    public string EnteredPin
    {
        get => _enteredPin;
        set { _enteredPin = value; OnPropertyChanged(); }
    }

    public bool IsCoolingDown
    {
        get => _isCoolingDown;
        set { _isCoolingDown = value; OnPropertyChanged(); }
    }

    public int CooldownSecondsRemaining { get; private set; }

    public ICommand SubmitCommand { get; }
    public event EventHandler? UnlockSucceeded;

    public PinLockViewModel()
    {
        SubmitCommand = new Command(async () => await Submit());
    }

    private async Task Submit()
    {
        if (IsCoolingDown) return;
        ClearError();

        string? storedPin;
        try
        {
            storedPin = await SecureStorage.GetAsync("app_pin");
        }
        catch (Exception ex)
        {
            // SecureStorage can throw if the Android keystore entry is corrupt (e.g. after a restore).
            System.Diagnostics.Debug.WriteLine($"PIN read failed: {ex}");
            SetError("Could not read the stored PIN. Please restart the app.");
            return;
        }

        if (!string.IsNullOrEmpty(EnteredPin) && EnteredPin == storedPin)
        {
            _failedAttempts = 0;
            EnteredPin = string.Empty;
            UnlockSucceeded?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            _failedAttempts++;
            EnteredPin = string.Empty;
            if (_failedAttempts >= AppConstants.MaxFailedPinAttempts)
                await StartCooldown();
            else
                SetError($"Incorrect PIN. {AppConstants.MaxFailedPinAttempts - _failedAttempts} attempts remaining.");
        }
    }

    private async Task StartCooldown()
    {
        IsCoolingDown = true;
        SetError($"Too many failed attempts. Wait {AppConstants.PinCooldownSeconds} seconds.");
        for (int i = AppConstants.PinCooldownSeconds; i > 0; i--)
        {
            CooldownSecondsRemaining = i;
            OnPropertyChanged(nameof(CooldownSecondsRemaining));
            await Task.Delay(1000);
        }
        _failedAttempts = 0;
        IsCoolingDown = false;
        ClearError();
    }
}
