using HealthTracker.ViewModels;

namespace HealthTracker.Tests.ViewModels;

public class TodayScheduleItemTests
{
    // ── Logged + Taken ────────────────────────────────────────────────────────
    [Fact]
    public void WhenLoggedAndTaken_StatusText_IsTaken()
    {
        var item = new TodayScheduleItem { IsLogged = true, WasTaken = true };
        Assert.Equal("Taken", item.StatusText);
    }

    [Fact]
    public void WhenLoggedAndTaken_StatusIcon_IsCheckmark()
    {
        var item = new TodayScheduleItem { IsLogged = true, WasTaken = true };
        Assert.Equal("\u2705", item.StatusIcon);
    }

    [Fact]
    public void WhenLoggedAndTaken_StatusColor_IsGreen()
    {
        var item = new TodayScheduleItem { IsLogged = true, WasTaken = true };
        Assert.Equal("#1A8A4A", item.StatusColor);
    }

    [Fact]
    public void WhenLoggedAndTaken_TimeUntilText_IsTaken()
    {
        var item = new TodayScheduleItem { IsLogged = true, WasTaken = true };
        Assert.Equal("Taken", item.TimeUntilText);
    }

    [Fact]
    public void WhenLoggedAndTaken_CanActNow_IsFalse()
    {
        var item = new TodayScheduleItem { IsLogged = true, WasTaken = true };
        Assert.False(item.CanActNow);
    }

    // ── Logged + Missed (auto-logged, not taken) ─────────────────────────────────────────────────────
    [Fact]
    public void WhenLoggedAndMissed_StatusText_IsMissed()
    {
        var item = new TodayScheduleItem { IsLogged = true, WasTaken = false };
        Assert.Equal("Missed", item.StatusText);
    }

    [Fact]
    public void WhenLoggedAndMissed_StatusIcon_IsCross()
    {
        var item = new TodayScheduleItem { IsLogged = true, WasTaken = false };
        Assert.Equal("\u274c", item.StatusIcon);
    }

    [Fact]
    public void WhenLoggedAndMissed_StatusColor_IsRed()
    {
        var item = new TodayScheduleItem { IsLogged = true, WasTaken = false };
        Assert.Equal("#C0392B", item.StatusColor);
    }

    [Fact]
    public void WhenLoggedAndMissed_TimeUntilText_IsMissed()
    {
        var item = new TodayScheduleItem { IsLogged = true, WasTaken = false };
        Assert.Equal("Missed", item.TimeUntilText);
    }

    [Fact]
    public void WhenLoggedAndMissed_CanActNow_IsFalse()
    {
        var item = new TodayScheduleItem { IsLogged = true, WasTaken = false };
        Assert.False(item.CanActNow);
    }

    // ── Invalid schedule time ─────────────────────────────────────────────────
    [Fact]
    public void WhenInvalidScheduleTime_StatusText_IsUpcoming()
    {
        var item = new TodayScheduleItem { ScheduleTime = "not-a-time" };
        Assert.Equal("Upcoming", item.StatusText);
    }

    [Fact]
    public void WhenInvalidScheduleTime_CanActNow_IsFalse()
    {
        var item = new TodayScheduleItem { ScheduleTime = "not-a-time" };
        Assert.False(item.CanActNow);
    }

    [Fact]
    public void WhenInvalidScheduleTime_TimeUntilText_ReturnsScheduleTime()
    {
        var item = new TodayScheduleItem { ScheduleTime = "bad" };
        Assert.Equal("bad", item.TimeUntilText);
    }

    // ── IsLogged setter fires PropertyChanged ────────────────────────────────
    [Fact]
    public void IsLogged_WhenSet_FiresPropertyChangedForDependentProps()
    {
        var item = new TodayScheduleItem { ScheduleTime = "08:00" };
        var raised = new List<string>();
        item.PropertyChanged += (_, e) => raised.Add(e.PropertyName!);

        item.IsLogged = true;

        Assert.Contains(nameof(TodayScheduleItem.IsLogged), raised);
        Assert.Contains(nameof(TodayScheduleItem.CanActNow), raised);
        Assert.Contains(nameof(TodayScheduleItem.StatusText), raised);
        Assert.Contains(nameof(TodayScheduleItem.StatusIcon), raised);
        Assert.Contains(nameof(TodayScheduleItem.StatusColor), raised);
        Assert.Contains(nameof(TodayScheduleItem.TimeUntilText), raised);
    }

    // ── RefreshWindow fires all props ─────────────────────────────────────────
    [Fact]
    public void RefreshWindow_FiresAllComputedPropertyChangedEvents()
    {
        var item = new TodayScheduleItem { ScheduleTime = "09:00" };
        var raised = new List<string>();
        item.PropertyChanged += (_, e) => raised.Add(e.PropertyName!);

        item.RefreshWindow();

        Assert.Contains(nameof(TodayScheduleItem.CanActNow), raised);
        Assert.Contains(nameof(TodayScheduleItem.StatusText), raised);
        Assert.Contains(nameof(TodayScheduleItem.StatusIcon), raised);
        Assert.Contains(nameof(TodayScheduleItem.StatusColor), raised);
        Assert.Contains(nameof(TodayScheduleItem.TimeUntilText), raised);
    }
}
