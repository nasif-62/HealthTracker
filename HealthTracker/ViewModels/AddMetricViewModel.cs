using System.Windows.Input;
using HealthTracker.Constants;
using HealthTracker.Helpers;
using HealthTracker.Services;

namespace HealthTracker.ViewModels;

public class AddMetricViewModel : BaseViewModel
{
    private readonly MetricsService _service;
    private string _selectedType = MetricTypes.Weight;

    public string SelectedType
    {
        get => _selectedType;
        set
        {
            _selectedType = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsBloodPressure));
            OnPropertyChanged(nameof(Unit));
        }
    }

    public bool IsBloodPressure => SelectedType == MetricTypes.BloodPressure;
    public string Unit => MetricTypes.GetUnit(SelectedType);
    public string[] MetricTypeOptions => MetricTypes.All;

    public string ValueText { get; set; } = string.Empty;
    public string SecondValueText { get; set; } = string.Empty;
    public DateTime RecordedAt { get; set; } = DateTime.Now;
    public string? Notes { get; set; }

    public ICommand SaveCommand { get; }
    public event EventHandler? SavedSuccessfully;

    public AddMetricViewModel(MetricsService service)
    {
        _service = service;
        SaveCommand = new Command(async () => await Save());
    }

    private async Task Save()
    {
        if (IsBusy) return;
        IsBusy = true;
        ClearError();
        try
        {
            if (!ValidationHelper.TryParseDouble(ValueText, out var value)) { SetError("Enter a valid numeric value."); return; }
            double? second = null;
            if (IsBloodPressure)
            {
                if (!ValidationHelper.TryParseDouble(SecondValueText, out var sv)) { SetError("Enter a valid diastolic value."); return; }
                second = sv;
            }
            await _service.AddMetric(SelectedType, value, second, Unit,
                DateTimeHelper.ToDateTimeString(RecordedAt), Notes);
            SavedSuccessfully?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex) { SetError(ex.Message); System.Diagnostics.Debug.WriteLine(ex); }
        finally { IsBusy = false; }
    }
}
