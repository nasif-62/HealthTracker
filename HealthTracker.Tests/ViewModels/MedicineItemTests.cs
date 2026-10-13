using HealthTracker.Models;
using HealthTracker.ViewModels;

namespace HealthTracker.Tests.ViewModels;

public class MedicineItemTests
{
    private static MedicineItem Build(double doseProgress = 0, int streak = 0, int missed = 0,
        double stockDays = 30, string stockUnit = "Tablet")
        => new()
        {
            Medicine = new Medicine { Name = "Aspirin", StockCount = 10, StockUnit = stockUnit, StartDate = "2024-01-01", CreatedAt = "2024-01-01T00:00:00", IsActive = 1 },
            DoseProgress = doseProgress,
            DoseProgressText = "1 of 2 doses taken today",
            StockDaysRemaining = stockDays,
            Streak = streak,
            MissedThisWeek = missed,
        };

    // ── ProgressColor ────────────────────────────────────────────────────────
    [Fact]
    public void ProgressColor_WhenComplete_IsGreen()
        => Assert.Equal("#1A8A4A", Build(doseProgress: 1.0).ProgressColor);

    [Fact]
    public void ProgressColor_WhenExceedsOne_IsGreen()
        => Assert.Equal("#1A8A4A", Build(doseProgress: 1.5).ProgressColor);

    [Fact]
    public void ProgressColor_WhenPartial_IsBlue()
        => Assert.Equal("#1B5FA8", Build(doseProgress: 0.5).ProgressColor);

    [Fact]
    public void ProgressColor_WhenZero_IsGrey()
        => Assert.Equal("#C8D8EA", Build(doseProgress: 0).ProgressColor);

    // ── Stock ────────────────────────────────────────────────────────────────
    [Theory]
    [InlineData(30, false, false)]
    [InlineData(7, false, false)]
    [InlineData(6.9, true, false)]
    [InlineData(4.9, true, true)]
    [InlineData(0, true, true)]
    public void StockFlags_FollowDaysRemainingThresholds(double days, bool low, bool critical)
    {
        var item = Build(stockDays: days);
        Assert.Equal(low, item.IsLowStock);
        Assert.Equal(critical, item.IsCriticalStock);
    }

    [Fact]
    public void StockWarningColor_IsRedWhenCriticalElseOrange()
    {
        Assert.Equal("#C0392B", Build(stockDays: 1).StockWarningColor);
        Assert.Equal("#E67E22", Build(stockDays: 6).StockWarningColor);
    }

    [Theory]
    [InlineData("Tablet", "💊")]
    [InlineData("Drop", "💧")]
    [InlineData("Unknown", "📦")]
    public void StockUnitIcon_MapsUnitToIcon(string unit, string expected)
        => Assert.Equal(expected, Build(stockUnit: unit).StockUnitIcon);

    // ── Streak ───────────────────────────────────────────────────────────────
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void ShowStreak_WhenStreakLessThan2_IsFalse(int streak)
        => Assert.False(Build(streak: streak).ShowStreak);

    [Theory]
    [InlineData(2)]
    [InlineData(10)]
    public void ShowStreak_WhenStreakAtLeast2_IsTrue(int streak)
        => Assert.True(Build(streak: streak).ShowStreak);

    [Fact]
    public void StreakText_ContainsStreakCount()
    {
        var item = Build(streak: 5);
        Assert.Contains("5", item.StreakText);
        Assert.Contains("streak", item.StreakText);
    }

    // ── Missed ───────────────────────────────────────────────────────────────
    [Fact]
    public void ShowMissed_WhenZeroMissed_IsFalse()
        => Assert.False(Build(missed: 0).ShowMissed);

    [Fact]
    public void ShowMissed_WhenMissedGreaterThanZero_IsTrue()
        => Assert.True(Build(missed: 2).ShowMissed);

    [Fact]
    public void MissedText_ContainsMissedCount()
    {
        var item = Build(missed: 3);
        Assert.Contains("3", item.MissedText);
        Assert.Contains("missed", item.MissedText);
    }
}
