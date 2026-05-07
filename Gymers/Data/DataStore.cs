using System.Collections.ObjectModel;
using Gymers.Models;

namespace Gymers.Data;

public sealed class DataStore
{
    public ObservableCollection<Member>  Members  { get; }
    public ObservableCollection<Payment> Payments { get; }
    public ObservableCollection<CheckIn> CheckIns { get; }

    public DataStore()
    {
        Members  = new ObservableCollection<Member>(SampleData.Members);
        Payments = new ObservableCollection<Payment>(
            SampleData.Payments.OrderByDescending(p => p.At));
        CheckIns = new ObservableCollection<CheckIn>(
            SampleData.CheckIns.OrderByDescending(c => c.At));
    }

    public Member? FindMemberByName(string? name) =>
        Members.FirstOrDefault(m =>
            string.Equals(m.Name, name?.Trim(), StringComparison.OrdinalIgnoreCase));

    public IEnumerable<Member> SearchMembers(string? query) =>
        string.IsNullOrWhiteSpace(query)
            ? Members
            : Members.Where(m =>
                m.Name.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase));

    public Payment RecordPayment(Member m, decimal amount, string method)
    {
        int nextId      = Payments.Count == 0 ? 1043 : Payments.Max(p => p.Id) + 1;
        int nextReceipt = Payments.Count == 0 ? 1043 : Payments.Max(p => p.ReceiptNumber) + 1;
        var p = new Payment(nextId, m.Id, amount, method, nextReceipt, DateTime.Now);
        Payments.Insert(0, p);
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
