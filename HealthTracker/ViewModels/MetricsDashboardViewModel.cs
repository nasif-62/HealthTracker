using System.Collections.ObjectModel;
using System.Windows.Input;
using HealthTracker.Constants;
using HealthTracker.Services;

namespace HealthTracker.ViewModels;

public class MetricsDashboardViewModel : BaseViewModel
{
    private readonly MetricsService _service;
    public ObservableCollection<MetricSummaryItem> Summaries { get; } = new();
    public ICommand LoadCommand { get; }

    public MetricsDashboardViewModel(MetricsService service)
    {
        _service = service;
        LoadCommand = new Command(async () => await Load());
    }

    public async Task Load()
    {
        if (IsBusy) return;
        IsBusy = true; ClearError();
        try
        {
            Summaries.Clear();
            foreach (var type in MetricTypes.All)
            {
                var history = await _service.GetHistoryByType(type, 2);
                var latest = history.Count >= 1 ? history[0] : null;
                var prev = history.Count >= 2 ? history[1] : null;

                var valueText = latest is null ? "\u2014" :
                    type == MetricTypes.BloodPressure
                        ? $"{latest.Value:0}/{latest.SecondValue:0}"
                        : latest.Value.ToString("0.#");

                var trend = string.Empty;
                if (latest != null && prev != null)
                {
                    var diff = latest.Value - prev.Value;
                    trend = Math.Abs(diff) < 0.05 ? "Stable" : diff > 0 ? $"\u2191 +{diff:0.#}" : $"\u2193 {diff:0.#}";
                }

                Summaries.Add(new MetricSummaryItem
                {
                    Type = type,
                    Icon = GetIcon(type),
                    DisplayName = GetDisplayName(type),
                    LatestValueText = valueText,
                    Unit = MetricTypes.GetUnit(type),
                    AccentColor = GetColor(type),
                    TrendText = trend,
                    HasData = latest != null
                });
            }
        }
        catch (Exception ex) { SetError("Failed to load metrics."); System.Diagnostics.Debug.WriteLine(ex); }
        finally { IsBusy = false; }
    }

    private static string GetIcon(string type) => type switch
    {
        MetricTypes.Weight => "\u2696",
        MetricTypes.BloodPressure => "\U0001fa7a",
        MetricTypes.HeartRate => "\u2665",
        MetricTypes.BloodSugar => "\U0001fa78",
        MetricTypes.SpO2 => "\U0001fac1",
        MetricTypes.Temperature => "\U0001f321",
        _ => "\U0001f4ca"
    };

    private static string GetDisplayName(string type) => type switch
    {
        MetricTypes.BloodPressure => "Blood Pressure",
        MetricTypes.HeartRate => "Heart Rate",
        MetricTypes.BloodSugar => "Blood Sugar",
        _ => type
    };

    public static string GetColor(string type) => type switch
    {
        MetricTypes.Weight => "#1B5FA8",
        MetricTypes.BloodPressure => "#C0392B",
        MetricTypes.HeartRate => "#E74C3C",
        MetricTypes.BloodSugar => "#F39C12",
        MetricTypes.SpO2 => "#27AE60",
        MetricTypes.Temperature => "#8E44AD",
        _ => "#1B5FA8"
    };
}
