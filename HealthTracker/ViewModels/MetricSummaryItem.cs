namespace HealthTracker.ViewModels;

public class MetricSummaryItem
{
    public string Type { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string LatestValueText { get; set; } = "\u2014";
    public string Unit { get; set; } = string.Empty;
    public string AccentColor { get; set; } = "#1B5FA8";
    public string TrendText { get; set; } = string.Empty;
    public bool HasData { get; set; }
    public bool HasTrend => HasData && !string.IsNullOrEmpty(TrendText);
}
