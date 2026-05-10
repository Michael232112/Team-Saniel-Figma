using System.Globalization;
using System.Text;
using Gymers.Models;

namespace Gymers.Services;

public static class CsvWriter
{
    static readonly char[] MustQuote = { ',', '"', '\r', '\n' };

    public static void WriteRevenue(string path, IEnumerable<(Payment Payment, Member? Member)> rows)
    {
        using var w = OpenWriter(path);
        w.WriteLine("date,receipt,member_name,member_id,method,amount");
        foreach (var (p, m) in rows)
        {
            w.WriteLine(string.Join(',',
                Quote(p.At.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
                Quote(p.ReceiptNumber.ToString(CultureInfo.InvariantCulture)),
                Quote(m?.Name ?? "(removed)"),
                Quote(p.MemberId),
                Quote(p.Method),
                Quote(p.Amount.ToString("0.00", CultureInfo.InvariantCulture))));
        }
    }

    public static void WriteAttendance(string path, IEnumerable<(CheckIn CheckIn, Member? Member)> rows)
    {
        using var w = OpenWriter(path);
        w.WriteLine("date,time,member_name,member_id");
        foreach (var (c, m) in rows)
        {
            w.WriteLine(string.Join(',',
                Quote(c.At.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                Quote(c.At.ToString("HH:mm:ss", CultureInfo.InvariantCulture)),
                Quote(m?.Name ?? "(removed)"),
                Quote(c.MemberId)));
        }
    }

    public static void WriteRoster(string path, IEnumerable<Member> rows)
    {
        using var w = OpenWriter(path);
        w.WriteLine("id,name,tier,status,expires");
        foreach (var m in rows)
        {
            w.WriteLine(string.Join(',',
                Quote(m.Id),
                Quote(m.Name),
                Quote(m.Tier.ToString()),
                Quote(m.Status),
                Quote(m.Expires.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))));
        }
    }

    static StreamWriter OpenWriter(string path) =>
        new(path, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
        {
            NewLine = "\n"
        };

    static string Quote(string value)
    {
        if (value.IndexOfAny(MustQuote) < 0) return value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
