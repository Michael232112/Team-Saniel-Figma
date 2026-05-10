using System.Globalization;
using Gymers.Data;
using Gymers.Models;
using Microsoft.Maui.Storage;

namespace Gymers.Services;

public sealed class ReportService
{
    readonly DataStore _data;

    public ReportService(DataStore data) => _data = data;

    public Task<string> GeneratePdfAsync(ReportKind kind, ReportPeriod period)
    {
        var path = BuildPath(kind, period, "pdf");
        var doc  = new ReportDocument(
            kind, period, DateTime.Now,
            _data.Members.ToList(),
            _data.Payments.ToList(),
            _data.CheckIns.ToList());
        doc.WritePdf(path);
        return Task.FromResult(path);
    }

    public Task<string> GenerateCsvAsync(ReportKind kind, ReportPeriod period)
    {
        var path = BuildPath(kind, period, "csv");
        var now  = DateTime.Now;

        switch (kind)
        {
            case ReportKind.Revenue:
            {
                var (from, to) = period.Range(now);
                var rows = _data.Payments
                    .Where(p => p.At >= from && p.At < to)
                    .OrderByDescending(p => p.At)
                    .Select(p => (p, _data.Members.FirstOrDefault(m => m.Id == p.MemberId)));
                CsvWriter.WriteRevenue(path, rows);
                break;
            }
            case ReportKind.Attendance:
            {
                var (from, to) = period.Range(now);
                var rows = _data.CheckIns
                    .Where(c => c.At >= from && c.At < to)
                    .OrderByDescending(c => c.At)
                    .Select(c => (c, _data.Members.FirstOrDefault(m => m.Id == c.MemberId)));
                CsvWriter.WriteAttendance(path, rows);
                break;
            }
            case ReportKind.Roster:
            {
                var rows = _data.Members.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase);
                CsvWriter.WriteRoster(path, rows);
                break;
            }
        }

        return Task.FromResult(path);
    }

    public string Summarize(ReportKind kind, ReportPeriod period)
    {
        var now = DateTime.Now;
        switch (kind)
        {
            case ReportKind.Revenue:
            {
                var (from, to) = period.Range(now);
                var rows  = _data.Payments.Where(p => p.At >= from && p.At < to).ToList();
                var total = rows.Sum(p => p.Amount);
                return rows.Count == 0
                    ? "No payments in this period."
                    : $"${total.ToString("0.00", CultureInfo.InvariantCulture)} from {rows.Count} payment{(rows.Count == 1 ? "" : "s")}";
            }
            case ReportKind.Attendance:
            {
                var (from, to) = period.Range(now);
                var rows   = _data.CheckIns.Where(c => c.At >= from && c.At < to).ToList();
                var unique = rows.Select(c => c.MemberId).Distinct().Count();
                return rows.Count == 0
                    ? "No check-ins in this period."
                    : $"{rows.Count} check-in{(rows.Count == 1 ? "" : "s")}, {unique} unique member{(unique == 1 ? "" : "s")}";
            }
            case ReportKind.Roster:
            {
                int active = _data.Members.Count(m => string.Equals(m.Status, "Active", StringComparison.OrdinalIgnoreCase));
                int other  = _data.Members.Count - active;
                return $"{active} active, {other} other";
            }
            default:
                return "—";
        }
    }

    static string BuildPath(ReportKind kind, ReportPeriod period, string ext)
    {
        var dir = Path.Combine(FileSystem.CacheDirectory, "reports");
        Directory.CreateDirectory(dir);
        var stamp = DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var name  = $"gymers-report-{kind.Slug()}-{period.Slug()}-{stamp}.{ext}";
        return Path.Combine(dir, name);
    }
}
