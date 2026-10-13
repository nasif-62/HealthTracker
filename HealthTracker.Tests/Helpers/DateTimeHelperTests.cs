using HealthTracker.Helpers;

namespace HealthTracker.Tests.Helpers;

public class DateTimeHelperTests
{
    [Fact]
    public void ToDateString_FormatsAsYyyyMmDd()
    {
        var dt = new DateTime(2024, 3, 7);
        Assert.Equal("2024-03-07", DateTimeHelper.ToDateString(dt));
    }

    [Fact]
    public void ToDateTimeString_FormatsWithTime()
    {
        var dt = new DateTime(2024, 3, 7, 14, 30, 5);
        Assert.Equal("2024-03-07T14:30:05", DateTimeHelper.ToDateTimeString(dt));
    }

    [Fact]
    public void ParseDate_ParsesDateOnlyString()
    {
        var result = DateTimeHelper.ParseDate("2024-03-07");
        Assert.Equal(new DateTime(2024, 3, 7), result.Date);
    }

    [Fact]
    public void ParseDateTime_ParsesFullDateTimeString()
    {
        var result = DateTimeHelper.ParseDateTime("2024-03-07T14:30:05");
        Assert.Equal(new DateTime(2024, 3, 7, 14, 30, 5), result);
    }

    [Fact]
    public void ToMonthYearGroup_ReturnsMonthAndYear()
    {
        var result = DateTimeHelper.ToMonthYearGroup("2024-03-07");
        Assert.Equal("March 2024", result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a date")]
    public void ToMonthYearGroup_WithBadInput_DoesNotThrow(string input)
    {
        Assert.Equal("Unknown", DateTimeHelper.ToMonthYearGroup(input));
    }

    [Fact]
    public void TryParse_HandlesDateAndDateTimeStringsAndRejectsEmpty()
    {
        Assert.True(DateTimeHelper.TryParse("2024-03-07", out var d));
        Assert.Equal(new DateTime(2024, 3, 7), d);
        Assert.True(DateTimeHelper.TryParse("2024-03-07T14:30:05", out var dt));
        Assert.Equal(new DateTime(2024, 3, 7, 14, 30, 5), dt);
        Assert.False(DateTimeHelper.TryParse(null, out _));
        Assert.False(DateTimeHelper.TryParse("", out _));
    }

    [Fact]
    public void RoundTrip_DateString_PreservesDate()
    {
        var original = new DateTime(2024, 11, 25);
        var str = DateTimeHelper.ToDateString(original);
        var parsed = DateTimeHelper.ParseDate(str);
        Assert.Equal(original, parsed.Date);
    }

    [Fact]
    public void RoundTrip_DateTimeString_PreservesDateTime()
    {
        var original = new DateTime(2024, 11, 25, 8, 45, 0);
        var str = DateTimeHelper.ToDateTimeString(original);
        var parsed = DateTimeHelper.ParseDateTime(str);
        Assert.Equal(original, parsed);
    }
}
