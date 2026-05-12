using System.Collections.ObjectModel;
using Gymers.Models;
using Microsoft.Maui.Storage;

namespace Gymers.Data;

public sealed class DataStore
{
    readonly GymersDb _db;

    public ObservableCollection<Member>      Members      { get; }
    public ObservableCollection<Payment>     Payments     { get; }
    public ObservableCollection<CheckIn>     CheckIns     { get; }
    public ObservableCollection<Trainer>     Trainers     { get; }
    public ObservableCollection<WorkoutPlan> WorkoutPlans { get; }
    public ObservableCollection<Equipment>   Equipment    { get; }

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

        if (_db.IsTrainersEmpty())
        {
            _db.SeedTrainers(SampleData.Trainers);
        }

        if (_db.IsWorkoutPlansEmpty())
        {
            _db.SeedWorkoutPlans(SampleData.WorkoutPlans);
        }

        if (_db.IsEquipmentEmpty())
        {
            _db.SeedEquipment(SampleData.Equipment);
        }

        Members      = new ObservableCollection<Member>(_db.GetMembersNewestFirst());
        Payments     = new ObservableCollection<Payment>(_db.GetPaymentsNewestFirst());
        CheckIns     = new ObservableCollection<CheckIn>(_db.GetCheckInsNewestFirst());
        Trainers     = new ObservableCollection<Trainer>(_db.GetTrainersNewestFirst());
        WorkoutPlans = new ObservableCollection<WorkoutPlan>(_db.GetWorkoutPlansOrdered());
        Equipment    = new ObservableCollection<Equipment>(_db.GetEquipmentOrdered());
    }

    public Member? FindMemberByName(string? name) =>
        Members.FirstOrDefault(m =>
            string.Equals(m.Name, name?.Trim(), StringComparison.OrdinalIgnoreCase));

    public IEnumerable<Member> SearchMembers(string? query) =>
        string.IsNullOrWhiteSpace(query)
            ? Members
            : Members.Where(m =>
                m.Name.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase));

    public IEnumerable<Trainer> SearchTrainers(string? query) =>
        string.IsNullOrWhiteSpace(query)
            ? Trainers
            : Trainers.Where(t =>
                t.Name.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase));

    public Trainer? TopTrainer() =>
        Trainers.OrderByDescending(t => t.Rating)
                .ThenByDescending(t => t.SessionsCompleted)
                .FirstOrDefault();

    public IEnumerable<WorkoutPlan> SearchWorkoutPlans(string? query) =>
        string.IsNullOrWhiteSpace(query)
            ? WorkoutPlans
            : WorkoutPlans.Where(p =>
                p.Name.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase));

    public WorkoutPlan? TopPlan() => WorkoutPlans.FirstOrDefault();

    public string TrainerName(string trainerId) =>
        Trainers.FirstOrDefault(t => t.Id == trainerId)?.Name ?? "—";

    public IEnumerable<Equipment> SearchEquipment(string? query) =>
        string.IsNullOrWhiteSpace(query)
            ? Equipment
            : Equipment.Where(e =>
                e.Name.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase));

    public int OperationalEquipmentCount() =>
        Equipment.Count(e => string.Equals(e.Status, "Operational", StringComparison.OrdinalIgnoreCase));

    public int MaintenanceEquipmentCount() =>
        Equipment.Count - OperationalEquipmentCount();

    public async Task<Member> AddMemberAsync(string name, MembershipTier tier, string status, DateOnly expires)
    {
        var member = new Member(NextId(Members.Select(m => m.Id), "m"), name.Trim(), tier, status, expires);
        await _db.InsertMemberAsync(member);
        await MainThread.InvokeOnMainThreadAsync(() => Members.Insert(0, member));
        return member;
    }

    public async Task UpdateMemberAsync(Member member)
    {
        await _db.UpdateMemberAsync(member);
        await ReplaceOnMainThreadAsync(Members, member, m => m.Id == member.Id);
    }

    public async Task DeleteMemberAsync(Member member)
    {
        await _db.DeleteMemberAsync(member);
        await MainThread.InvokeOnMainThreadAsync(() => Members.Remove(member));
    }

    public async Task<Trainer> AddTrainerAsync(string name, string title, decimal rating, int sessionsCompleted)
    {
        var trainer = new Trainer(NextId(Trainers.Select(t => t.Id), "t"), name.Trim(), title.Trim(), rating, sessionsCompleted);
        await _db.InsertTrainerAsync(trainer);
        await MainThread.InvokeOnMainThreadAsync(() => Trainers.Insert(0, trainer));
        return trainer;
    }

    public async Task UpdateTrainerAsync(Trainer trainer)
    {
        await _db.UpdateTrainerAsync(trainer);
        await ReplaceOnMainThreadAsync(Trainers, trainer, t => t.Id == trainer.Id);
    }

    public async Task DeleteTrainerAsync(Trainer trainer)
    {
        await _db.DeleteTrainerAsync(trainer);
        await MainThread.InvokeOnMainThreadAsync(() => Trainers.Remove(trainer));
    }

    public async Task<WorkoutPlan> AddWorkoutPlanAsync(
        string name,
        string trainerId,
        string level,
        int sessionsPerWeek,
        int durationWeeks,
        string summary)
    {
        int orderRank = WorkoutPlans.Count == 0 ? 1 : WorkoutPlans.Min(p => p.OrderRank) - 1;
        var plan = new WorkoutPlan(
            NextId(WorkoutPlans.Select(p => p.Id), "p"),
            name.Trim(),
            trainerId,
            level.Trim(),
            sessionsPerWeek,
            durationWeeks,
            summary.Trim(),
            orderRank);

        await _db.InsertWorkoutPlanAsync(plan);
        await MainThread.InvokeOnMainThreadAsync(() => WorkoutPlans.Insert(0, plan));
        return plan;
    }

    public async Task UpdateWorkoutPlanAsync(WorkoutPlan plan)
    {
        await _db.UpdateWorkoutPlanAsync(plan);
        await ReplaceOnMainThreadAsync(WorkoutPlans, plan, p => p.Id == plan.Id);
    }

    public async Task DeleteWorkoutPlanAsync(WorkoutPlan plan)
    {
        await _db.DeleteWorkoutPlanAsync(plan);
        await MainThread.InvokeOnMainThreadAsync(() => WorkoutPlans.Remove(plan));
    }

    public async Task<Equipment> AddEquipmentAsync(string name, string category, string status, string location)
    {
        int orderRank = Equipment.Count == 0 ? 1 : Equipment.Min(e => e.OrderRank) - 1;
        var item = new Equipment(
            NextId(Equipment.Select(e => e.Id), "e"),
            name.Trim(),
            category.Trim(),
            status.Trim(),
            location.Trim(),
            orderRank);

        await _db.InsertEquipmentAsync(item);
        await MainThread.InvokeOnMainThreadAsync(() => Equipment.Insert(0, item));
        return item;
    }

    public async Task UpdateEquipmentAsync(Equipment item)
    {
        await _db.UpdateEquipmentAsync(item);
        await ReplaceOnMainThreadAsync(Equipment, item, e => e.Id == item.Id);
    }

    public async Task DeleteEquipmentAsync(Equipment item)
    {
        await _db.DeleteEquipmentAsync(item);
        await MainThread.InvokeOnMainThreadAsync(() => Equipment.Remove(item));
    }

    public async Task<Payment> RecordPaymentAsync(Member m, decimal amount, string method)
    {
        int nextId      = Payments.Count == 0 ? 1043 : Payments.Max(p => p.Id) + 1;
        int nextReceipt = Payments.Count == 0 ? 1043 : Payments.Max(p => p.ReceiptNumber) + 1;
        var p = new Payment(nextId, m.Id, amount, method, nextReceipt, DateTime.Now);
        await _db.InsertPaymentAsync(p);
        await MainThread.InvokeOnMainThreadAsync(() => Payments.Insert(0, p));
        return p;
    }

    public async Task<CheckIn> RecordCheckInAsync(Member m)
    {
        int nextId = CheckIns.Count == 0 ? 1 : CheckIns.Max(c => c.Id) + 1;
        var c = new CheckIn(nextId, m.Id, DateTime.Now);
        await _db.InsertCheckInAsync(c);
        await MainThread.InvokeOnMainThreadAsync(() => CheckIns.Insert(0, c));
        return c;
    }

    public IEnumerable<Member> GetExpiringSoonMembers() =>
        Members.Where(m =>
            string.Equals(m.Status, "Expiring Soon", StringComparison.OrdinalIgnoreCase));

    static string NextId(IEnumerable<string> ids, string prefix)
    {
        int max = ids
            .Where(id => id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(id => int.TryParse(id[prefix.Length..], out int n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max();

        return $"{prefix}{max + 1}";
    }

    static Task ReplaceOnMainThreadAsync<T>(ObservableCollection<T> collection, T item, Func<T, bool> predicate) =>
        MainThread.InvokeOnMainThreadAsync(() =>
        {
            int index = collection.Select((value, i) => new { value, i })
                                  .FirstOrDefault(x => predicate(x.value))?.i ?? -1;
            if (index >= 0)
                collection[index] = item;
        });
}
