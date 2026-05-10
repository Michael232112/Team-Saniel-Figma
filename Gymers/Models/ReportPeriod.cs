namespace Gymers.Models;

public enum ReportPeriod
{
    Week,
    Month,
    All
}

public static class ReportPeriodExtensions
{
    public static (DateTime From, DateTime To) Range(this ReportPeriod period, DateTime now) =>
        period switch
        {
            ReportPeriod.Week  => (now.AddDays(-7), now.AddSeconds(1)),
            ReportPeriod.Month => (now.AddDays(-30), now.AddSeconds(1)),
            ReportPeriod.All   => (DateTime.MinValue, DateTime.MaxValue),
            _                  => throw new ArgumentOutOfRangeException(nameof(period))
        };

    public static string Label(this ReportPeriod period) =>
        period switch
        {
            ReportPeriod.Week  => "Week",
            ReportPeriod.Month => "Month",
            ReportPeriod.All   => "All",
            _                  => throw new ArgumentOutOfRangeException(nameof(period))
        };

    public static string Slug(this ReportPeriod period) =>
        period.ToString().ToLowerInvariant();
}
