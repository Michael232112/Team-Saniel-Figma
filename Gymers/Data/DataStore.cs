using System.Collections.ObjectModel;
using Gymers.Models;
using Microsoft.Maui.Storage;

namespace Gymers.Data;

public sealed class DataStore
{
    readonly GymersDb _db;

    public ObservableCollection<Member>  Members  { get; }
    public ObservableCollection<Payment> Payments { get; }
    public ObservableCollection<CheckIn> CheckIns { get; }

    public DataStore()
    {
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "gymers.db3");
        _db = new GymersDb(dbPath);

        if (_db.IsMembersEmpty())
        {
            _db.SeedMembers(SampleData.Members);
            _db.SeedPayments(SampleData.Payments);
            _db.SeedCheckIns(SampleData.CheckIns);
        }

        Members  = new ObservableCollection<Member>(_db.GetMembers());
        Payments = new ObservableCollection<Payment>(_db.GetPaymentsNewestFirst());
        CheckIns = new ObservableCollection<CheckIn>(_db.GetCheckInsNewestFirst());
    }

    public Member? FindMemberByName(string? name) =>
        Members.FirstOrDefault(m =>
            string.Equals(m.Name, name?.Trim(), StringComparison.OrdinalIgnoreCase));

    public IEnumerable<Member> SearchMembers(string? query) =>
        string.IsNullOrWhiteSpace(query)
            ? Members
            : Members.Where(m =>
                m.Name.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase));

    public async Task<Payment> RecordPaymentAsync(Member m, decimal amount, string method)
    {
        int nextId      = Payments.Count == 0 ? 1043 : Payments.Max(p => p.Id) + 1;
        int nextReceipt = Payments.Count == 0 ? 1043 : Payments.Max(p => p.ReceiptNumber) + 1;
        var p = new Payment(nextId, m.Id, amount, method, nextReceipt, DateTime.Now);
        await _db.InsertPaymentAsync(p);
        await MainThread.InvokeOnMainThreadAsync(() => Payments.Insert(0, p));
        return p;
    }

    public CheckIn RecordCheckIn(Member m)
    {
        int nextId = CheckIns.Count == 0 ? 1 : CheckIns.Max(c => c.Id) + 1;
        var c = new CheckIn(nextId, m.Id, DateTime.Now);
        CheckIns.Insert(0, c);
        return c;
    }
}
