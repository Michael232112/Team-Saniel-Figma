namespace Gymers.Data;

public static class TrainerSchedules
{
    static readonly Dictionary<string, string> _byId = new()
    {
        ["t1"] = "Mon/Wed/Fri · 6am–2pm",
        ["t2"] = "Tue/Thu/Sat · 5pm–10pm",
        ["t3"] = "Mon–Fri · 7am–3pm",
        ["t4"] = "Tue/Thu/Sat/Sun · 7am–12pm",
        ["t5"] = "Wed/Fri/Sun · 4pm–9pm",
    };

    public static string GetFor(string trainerId) =>
        _byId.TryGetValue(trainerId, out var s) ? s : "Schedule TBD";
}
