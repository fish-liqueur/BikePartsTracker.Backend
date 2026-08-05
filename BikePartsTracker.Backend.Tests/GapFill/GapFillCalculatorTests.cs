using BikePartsTracker.Services;

namespace BikePartsTracker.Backend.Tests.GapFill;

public class GapFillCalculatorTests
{
    private static readonly DateTime Today = new(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc);

    // G-01
    [Fact]
    public void Empty_watermark_fetches_full_lookback_window()
    {
        var periodStart = Today.AddDays(-30);
        var gaps = GapFillCalculator.ComputeMissingRanges(periodStart, Today, null, null);

        Assert.Single(gaps);
        Assert.Equal(Today.AddDays(-30), gaps[0].From);
        Assert.Equal(Today, gaps[0].To);

        var (from, to) = GapFillCalculator.ExpandWatermark(null, null, gaps[0].From, gaps[0].To);
        Assert.Equal(gaps[0].From, from);
        Assert.Equal(gaps[0].To, to);
    }

    // G-02
    [Fact]
    public void Period_start_35_days_ago_clips_to_floor()
    {
        var periodStart = Today.AddDays(-35);
        var gaps = GapFillCalculator.ComputeMissingRanges(periodStart, Today, null, null);

        Assert.Single(gaps);
        Assert.Equal(Today.AddDays(-30), gaps[0].From);
        Assert.Equal(Today, gaps[0].To);
    }

    // G-03
    [Fact]
    public void Second_older_period_enqueues_only_missing_older_slice()
    {
        var coveredFrom = Today.AddDays(-10);
        var coveredTo = Today;
        var periodStart = Today.AddDays(-25);

        var gaps = GapFillCalculator.ComputeMissingRanges(periodStart, Today, coveredFrom, coveredTo);

        Assert.Single(gaps);
        Assert.Equal(Today.AddDays(-25), gaps[0].From);
        Assert.Equal(Today.AddDays(-11), gaps[0].To);
    }

    // G-04
    [Fact]
    public void Watermark_already_covers_needed_range_enqueues_nothing()
    {
        var periodStart = Today.AddDays(-10);
        var gaps = GapFillCalculator.ComputeMissingRanges(
            periodStart, Today, Today.AddDays(-30), Today);

        Assert.Empty(gaps);
    }

    // G-05
    [Fact]
    public void Period_start_one_year_ago_only_floor_window()
    {
        var periodStart = Today.AddYears(-1);
        var gaps = GapFillCalculator.ComputeMissingRanges(periodStart, Today, null, null);

        Assert.Single(gaps);
        Assert.Equal(Today.AddDays(-30), gaps[0].From);
        Assert.Equal(Today, gaps[0].To);
    }

    // G-06
    [Fact]
    public void After_today_slides_still_respects_new_floor()
    {
        var nextDay = Today.AddDays(1);
        var periodStart = nextDay.AddDays(-31); // one day before new floor
        var gaps = GapFillCalculator.ComputeMissingRanges(
            periodStart, nextDay, Today.AddDays(-30), Today);

        // Need is [nextDay-30, nextDay]; watermark covers to Today (= nextDay-1).
        // Missing newer slice: nextDay only (after coveredTo).
        // Older than new floor is not requested.
        Assert.DoesNotContain(gaps, g => g.From < nextDay.AddDays(-30));
        Assert.All(gaps, g => Assert.True(g.From >= nextDay.AddDays(-30)));
    }

    [Fact]
    public void TryExpandWatermarkForDay_seeds_empty_and_grows_only_when_adjacent()
    {
        Assert.True(GapFillCalculator.TryExpandWatermarkForDay(
            null, null, Today, out var from, out var to));
        Assert.Equal(Today, from);
        Assert.Equal(Today, to);

        Assert.True(GapFillCalculator.TryExpandWatermarkForDay(
            Today, Today, Today.AddDays(1), out from, out to));
        Assert.Equal(Today, from);
        Assert.Equal(Today.AddDays(1), to);

        // Non-adjacent: no change (would falsely cover the hole)
        Assert.False(GapFillCalculator.TryExpandWatermarkForDay(
            Today, Today, Today.AddDays(2), out from, out to));
        Assert.Equal(Today, from);
        Assert.Equal(Today, to);

        // Already inside: no change
        Assert.False(GapFillCalculator.TryExpandWatermarkForDay(
            Today.AddDays(-2), Today.AddDays(1), Today, out from, out to));
        Assert.Equal(Today.AddDays(-2), from);
        Assert.Equal(Today.AddDays(1), to);
    }
}
