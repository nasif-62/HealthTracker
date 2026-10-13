using System.Collections.ObjectModel;
using System.Windows.Input;
using HealthTracker.Constants;
using HealthTracker.Models;
using HealthTracker.Services;
using Microcharts;
using SkiaSharp;

namespace HealthTracker.ViewModels;

public class MetricHistoryViewModel : BaseViewModel
{
    private readonly MetricsService _service;
    private string _metricType = MetricTypes.Weight;

    public string MetricType
    {
        get => _metricType;
        set { _metricType = value; OnPropertyChanged(); OnPropertyChanged(nameof(Unit)); }
    }

    public string Unit => MetricTypes.GetUnit(_metricType);
    public ObservableCollection<HealthMetric> History { get; } = new();

    public Chart? MetricChart { get; private set; }
    public bool HasChart => MetricChart != null;
    public string LatestText { get; private set; } = "\u2014";
    public string MinText { get; private set; } = "\u2014";
    public string MaxText { get; private set; } = "\u2014";

    public ICommand DeleteCommand { get; }

    public MetricHistoryViewModel(MetricsService service)
    {
        _service = service;
        DeleteCommand = new Command<HealthMetric>(async m => await Delete(m));
    }

    private async Task Delete(HealthMetric metric)
    {
        try
        {
            await _service.DeleteMetric(metric);
            History.Remove(metric);
        }
        catch (Exception ex) { SetError("Failed to delete."); System.Diagnostics.Debug.WriteLine(ex); }
    }

    public async Task Load(string metricType)
    {
        if (IsBusy) return;
        MetricType = metricType;
        IsBusy = true; ClearError();
        try
        {
            var list = await _service.GetHistoryByType(metricType, 30);
            History.Clear();
            foreach (var m in list) History.Add(m);

            if (list.Count > 0)
            {
                var unit = MetricTypes.GetUnit(metricType);
                var latest = list[0];
                var min = list.Min(m => m.Value);
                var max = list.Max(m => m.Value);

                LatestText = metricType == MetricTypes.BloodPressure
                    ? $"{latest.Value:0}/{latest.SecondValue:0} {unit}"
                    : $"{latest.Value:0.#} {unit}";
                MinText = $"{min:0.#} {unit}";
                MaxText = $"{max:0.#} {unit}";

                var color = SKColor.Parse(MetricsDashboardViewModel.GetColor(metricType));
                var entries = list
                    .OrderBy(m => m.RecordedAt)
                    .Select(m => new ChartEntry((float)m.Value)
                    {
                        Label = m.RecordedAt.Length >= 10 ? m.RecordedAt[5..10] : m.RecordedAt,
                        ValueLabel = m.Value.ToString("0.#"),
                        Color = color,
                        TextColor = SKColor.Parse("#8C9EAD"),
                        ValueLabelColor = color
                    })
                    .ToList();

                MetricChart = new LineChart
                {
                    Entries = entries,
                    LineMode = LineMode.Spline,
                    LineSize = 3,
                    PointMode = PointMode.Circle,
                    PointSize = 8,
                    BackgroundColor = SKColors.Transparent,
                    LabelTextSize = 28,
                    ValueLabelTextSize = 28,
                    LabelOrientation = Orientation.Horizontal,
                    ValueLabelOrientation = Orientation.Horizontal,
                    IsAnimated = true
                };
            }
            else
            {
                MetricChart = null;
                LatestText = MinText = MaxText = "\u2014";
            }

            OnPropertyChanged(nameof(MetricChart));
            OnPropertyChanged(nameof(HasChart));
            OnPropertyChanged(nameof(LatestText));
            OnPropertyChanged(nameof(MinText));
            OnPropertyChanged(nameof(MaxText));
        }
        catch (Exception ex) { SetError("Failed to load history."); System.Diagnostics.Debug.WriteLine(ex); }
        finally { IsBusy = false; }
    }
}
