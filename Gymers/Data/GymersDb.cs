using System.Globalization;
using Gymers.Data.Rows;
using Gymers.Models;
using SQLite;

namespace Gymers.Data;

public sealed class GymersDb
{
    readonly string _path;
    readonly SQLiteConnection _sync;
    SQLiteAsyncConnection? _async;

    public GymersDb(string path)
    {
        _path = path;
        _sync = new SQLiteConnection(path);
        _sync.CreateTable<MemberRow>();
        _sync.CreateTable<PaymentRow>();
        _sync.CreateTable<CheckInRow>();
    }

    public SQLiteAsyncConnection Async => _async ??= new SQLiteAsyncConnection(_path);

    public bool IsMembersEmpty() =>
        _sync.Table<MemberRow>().Count() == 0;

    public void SeedMembers(IEnumerable<Member> members)
    {
        foreach (var m in members) _sync.Insert(ToRow(m));
    }

    public void SeedPayments(IEnumerable<Payment> payments)
    {
        foreach (var p in payments) _sync.Insert(ToRow(p));
    }

    public void SeedCheckIns(IEnumerable<CheckIn> checkIns)
    {
        foreach (var c in checkIns) _sync.Insert(ToRow(c));
    }

    public IEnumerable<Member> GetMembers() =>
        _sync.Table<MemberRow>().ToList().Select(ToRecord);

    public IEnumerable<Payment> GetPaymentsNewestFirst() =>
        _sync.Table<PaymentRow>()
             .OrderByDescending(r => r.AtTicks)
             .ToList()
             .Select(ToRecord);

    public IEnumerable<CheckIn> GetCheckInsNewestFirst() =>
        _sync.Table<CheckInRow>()
             .OrderByDescending(r => r.AtTicks)
             .ToList()
             .Select(ToRecord);

    public Task InsertPaymentAsync(Payment p) => Async.InsertAsync(ToRow(p));
    public Task InsertCheckInAsync(CheckIn c) => Async.InsertAsync(ToRow(c));

    static MemberRow ToRow(Member m) => new()
    {
        Id         = m.Id,
        Name       = m.Name,
        Tier       = (int)m.Tier,
        Status     = m.Status,
        ExpiresIso = m.Expires.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
    };

    static Member ToRecord(MemberRow r) => new(
        r.Id, r.Name, (MembershipTier)r.Tier, r.Status,
        DateOnly.ParseExact(r.ExpiresIso, "yyyy-MM-dd", CultureInfo.InvariantCulture));

    static PaymentRow ToRow(Payment p) => new()
    {
        Id            = p.Id,
        MemberId      = p.MemberId,
        AmountText    = p.Amount.ToString(CultureInfo.InvariantCulture),
        Method        = p.Method,
        ReceiptNumber = p.ReceiptNumber,
        AtTicks       = p.At.Ticks
    };

    static Payment ToRecord(PaymentRow r) => new(
        r.Id,
        r.MemberId,
        decimal.Parse(r.AmountText, CultureInfo.InvariantCulture),
        r.Method,
        r.ReceiptNumber,
        new DateTime(r.AtTicks));

    static CheckInRow ToRow(CheckIn c) => new()
    {
        Id       = c.Id,
        MemberId = c.MemberId,
        AtTicks  = c.At.Ticks
    };

    static CheckIn ToRecord(CheckInRow r) => new(
        r.Id, r.MemberId, new DateTime(r.AtTicks));
}
