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

        Members      = new ObservableCollection<Member>(_db.GetMembers());
        Payments     = new ObservableCollection<Payment>(_db.GetPaymentsNewestFirst());
        CheckIns     = new ObservableCollection<CheckIn>(_db.GetCheckInsNewestFirst());
        Trainers     = new ObservableCollection<Trainer>(_db.GetTrainersByRatingDesc());
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

    public Trainer? TopTrainer() => Trainers.FirstOrDefault();

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
}
