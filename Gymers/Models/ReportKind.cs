namespace Gymers.Models;

public enum ReportKind
{
    Revenue,
    Attendance,
    Roster
}

public static class ReportKindExtensions
{
    public static string Label(this ReportKind kind) =>
        kind switch
        {
            ReportKind.Revenue    => "Revenue",
            ReportKind.Attendance => "Attendance",
            ReportKind.Roster     => "Member Roster",
            _                     => throw new ArgumentOutOfRangeException(nameof(kind))
        };

    public static string Slug(this ReportKind kind) =>
        kind switch
        {
            ReportKind.Revenue    => "revenue",
            ReportKind.Attendance => "attendance",
            ReportKind.Roster     => "roster",
            _                     => throw new ArgumentOutOfRangeException(nameof(kind))
        };
}
