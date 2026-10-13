using HealthTracker.Models;

namespace HealthTracker.ViewModels;

public class MedicineItem
{
    public Medicine Medicine { get; set; } = null!;

    public string StockUnitIcon => Medicine.StockUnit switch
    {
        "Tablet"    => "💊",
        "Capsule"   => "💊",
        "Drop"      => "💧",
        "Syrup"     => "🧴",
        "Injection" => "💉",
        "Spray"     => "🚿",
        "Inhaler"   => "🫁",
        _           => "📦"
    };
    public double DoseProgress { get; set; }
    public string DoseProgressText { get; set; } = string.Empty;
    public double StockDaysRemaining { get; set; }
    public bool IsLowStock => StockDaysRemaining < 7;
    public bool IsCriticalStock => StockDaysRemaining < 5;
    public string StockWarningColor => IsCriticalStock ? "#C0392B" : "#E67E22";
    public string ProgressColor => DoseProgress >= 1.0 ? "#1A8A4A" : DoseProgress > 0 ? "#1B5FA8" : "#C8D8EA";

    public int Streak { get; set; }
    public int MissedThisWeek { get; set; }

    public bool ShowStreak => Streak >= 2;
    public bool ShowMissed => MissedThisWeek > 0;
    public string StreakText => $"\U0001f525 {Streak} day streak";
    public string MissedText => $"\u26a0 {MissedThisWeek} missed this week";
}
