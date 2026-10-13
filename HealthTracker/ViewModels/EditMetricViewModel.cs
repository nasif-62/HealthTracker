using System.Globalization;
using System.Windows.Input;
using HealthTracker.Constants;
using HealthTracker.Helpers;
using HealthTracker.Models;
using HealthTracker.Services;

namespace HealthTracker.ViewModels;

public class EditMetricViewModel : BaseViewModel
{
    private readonly MetricsService _service;
    private HealthMetric? _metric;

    public string MetricType { get; private set; } = string.Empty;
    public bool IsBloodPressure => MetricType == MetricTypes.BloodPressure;
    public string Unit => MetricTypes.GetUnit(MetricType);

    public string ValueText { get; set; } = string.Empty;
    public string SecondValueText { get; set; } = string.Empty;
    public DateTime RecordedAt { get; set; } = DateTime.Now;
    public string? Notes { get; set; }

    public ICommand SaveCommand { get; }
    public event EventHandler? SavedSuccessfully;

    public EditMetricViewModel(MetricsService service)
    {
        _service = service;
        SaveCommand = new Command(async () => await Save());
    }

    public void Load(HealthMetric metric)
    {
        // Work on a copy so a failed validation never corrupts the instance shown in the history list.
        _metric = new HealthMetric
        {
            Id = metric.Id,
            MetricType = metric.MetricType,
            Value = metric.Value,
            SecondValue = metric.SecondValue,
            Unit = metric.Unit,
            RecordedAt = metric.RecordedAt,
            Notes = metric.Notes
        };
        MetricType = metric.MetricType;
        ValueText = metric.Value.ToString("0.##", CultureInfo.InvariantCulture);
        SecondValueText = metric.SecondValue?.ToString("0.##", CultureInfo.InvariantCulture) ?? string.Empty;
        RecordedAt = DateTimeHelper.TryParse(metric.RecordedAt, out var dt) ? dt : DateTime.Now;
        Notes = metric.Notes;

        OnPropertyChanged(nameof(MetricType));
        OnPropertyChanged(nameof(IsBloodPressure));
        OnPropertyChanged(nameof(Unit));
        OnPropertyChanged(nameof(ValueText));
        OnPropertyChanged(nameof(SecondValueText));
        OnPropertyChanged(nameof(RecordedAt));
        OnPropertyChanged(nameof(Notes));
    }

    private async Task Save()
    {
        if (IsBusy || _metric == null) return;
        IsBusy = true; ClearError();
        try
        {
            if (!ValidationHelper.TryParseDouble(ValueText, out var value)) { SetError("Enter a valid numeric value."); return; }
            double? second = null;
            if (IsBloodPressure)
            {
                if (!ValidationHelper.TryParseDouble(SecondValueText, out var sv)) { SetError("Enter a valid diastolic value."); return; }
                second = sv;
            }

            _metric.Value = value;
            _metric.SecondValue = second;
            _metric.RecordedAt = DateTimeHelper.ToDateTimeString(RecordedAt);
            _metric.Notes = Notes;

            await _service.UpdateMetric(_metric);
            SavedSuccessfully?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex) { SetError(ex.Message); System.Diagnostics.Debug.WriteLine(ex); }
        finally { IsBusy = false; }
    }
}
