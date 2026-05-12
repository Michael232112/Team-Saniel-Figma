using Gymers.Models;

namespace Gymers.Data;

public static class SampleData
{
    public static readonly IReadOnlyList<Member> Members = new[]
    {
        new Member("m1", "Marcus Sterling", MembershipTier.Premium, "Active",         new DateOnly(2026, 12, 15)),
        new Member("m2", "Lena Park",       MembershipTier.Elite,   "Active",         new DateOnly(2027,  3,  4)),
        new Member("m3", "Diego Alvarez",   MembershipTier.Basic,   "Active",         new DateOnly(2026,  6, 22)),
        new Member("m4", "Aisha Khan",      MembershipTier.Premium, "Active",         new DateOnly(2026, 11,  1)),
        new Member("m5", "Sam Chen",        MembershipTier.Basic,   "Expiring Soon",  new DateOnly(2026,  5, 30)),
        new Member("m6", "Priya Nair",      MembershipTier.Elite,   "Active",         new DateOnly(2027,  8, 14)),
    };

    public static readonly IReadOnlyList<Payment> Payments = new[]
    {
        new Payment(1042, "m1", 99.00m,  "Card", 1042, new DateTime(2026, 5, 5,  9, 41, 0)),
        new Payment(1041, "m2", 149.00m, "Card", 1041, new DateTime(2026, 5, 5,  9, 12, 0)),
        new Payment(1040, "m3", 49.00m,  "Cash", 1040, new DateTime(2026, 5, 5,  8, 55, 0)),
        new Payment(1039, "m4", 99.00m,  "Bank", 1039, new DateTime(2026, 5, 4, 18, 22, 0)),
        new Payment(1038, "m5", 49.00m,  "Cash", 1038, new DateTime(2026, 5, 4, 17, 03, 0)),
    };

    public static readonly IReadOnlyList<CheckIn> CheckIns = new[]
    {
        new CheckIn(1, "m1", new DateTime(2026, 5, 5, 9, 42, 0)),
        new CheckIn(2, "m2", new DateTime(2026, 5, 5, 9, 38, 0)),
        new CheckIn(3, "m3", new DateTime(2026, 5, 5, 9, 21, 0)),
        new CheckIn(4, "m4", new DateTime(2026, 5, 5, 9, 15, 0)),
        new CheckIn(5, "m6", new DateTime(2026, 5, 5, 9, 8,  0)),
        new CheckIn(6, "m5", new DateTime(2026, 5, 5, 8, 51, 0)),
    };

    public static readonly IReadOnlyList<ClassSession> TodaysClasses = new[]
    {
        new ClassSession("c1", "High-Intensity Power Blast", "Studio A",
            new DateTime(2026, 5, 5, 10, 30, 0), new DateTime(2026, 5, 5, 11, 30, 0)),
        new ClassSession("c2", "Zen Flow Vinyasa", "Yoga Loft",
            new DateTime(2026, 5, 5, 12, 00, 0), new DateTime(2026, 5, 5, 13, 15, 0)),
        new ClassSession("c3", "Advanced Squat Workshop", "Performance Zone",
            new DateTime(2026, 5, 5, 13, 30, 0), new DateTime(2026, 5, 5, 15, 00, 0)),
    };

    public static readonly IReadOnlyList<Trainer> Trainers = new[]
    {
        new Trainer("t1", "Marcus Sterling", "Lead Performance Coach", 4.9m, 142),
        new Trainer("t2", "Sienna Vega",     "HIIT Specialist",        4.8m, 118),
        new Trainer("t3", "Rohan Iyer",      "Strength Coach",         4.7m,  96),
        new Trainer("t4", "Maya Okafor",     "Yoga Instructor",        4.7m,  88),
        new Trainer("t5", "Caleb Whit",      "Mobility Coach",         4.5m,  64),
    };

    public static readonly IReadOnlyList<WorkoutPlan> WorkoutPlans = new[]
    {
        new WorkoutPlan("p1", "Foundations of Strength",  "t1", "Beginner",     3, 6,
            "Compound lifts and bracing fundamentals for new gym members.",        1),
        new WorkoutPlan("p2", "HIIT Conditioning Cycle",  "t2", "Intermediate", 4, 4,
            "Four-week conditioning block built around 20-min HIIT circuits.",     2),
        new WorkoutPlan("p3", "Power Build 8-Week",       "t3", "Advanced",     5, 8,
            "Heavy-day / volume-day split for intermediate-to-advanced lifters.",  3),
        new WorkoutPlan("p4", "Mindful Mobility Series",  "t4", "Beginner",     3, 6,
            "Yoga-anchored mobility and breathwork; recovery between sessions.",   4),
        new WorkoutPlan("p5", "Active Recovery Block",    "t5", "Intermediate", 3, 4,
            "Low-intensity flow + mobility for deload weeks.",                     5),
    };

    public static readonly IReadOnlyList<Equipment> Equipment = new[]
    {
        new Equipment("e1", "Treadmill TR-01",     "Cardio",   "Operational", "Cardio Zone",  1),
        new Equipment("e2", "Treadmill TR-02",     "Cardio",   "Operational", "Cardio Zone",  2),
        new Equipment("e3", "Power Rack PR-A",     "Strength", "Operational", "Weight Room",  3),
        new Equipment("e4", "Smith Machine SM-01", "Strength", "Maintenance", "Weight Room",  4),
        new Equipment("e5", "Spin Bike SB-03",     "Cardio",   "Operational", "Cardio Zone",  5),
        new Equipment("e6", "Yoga Mat Set YM-01",  "Studio",   "Operational", "Yoga Studio",  6),
    };

    public static Member GetMember(string id) =>
        Members.First(m => m.Id == id);
}
