using TheTimer.Core;
using TheTimer.Core.Planning;
using TheTimer.Core.Sessions;

namespace TheTimer.Screenshots;

/// <summary>Seeded demo data resembling the Stitch mockups. Fake "now" is Monday 2026-10-12 15:32.</summary>
public static class DemoData
{
    public static readonly DateOnly Today = new(2026, 10, 12);
    public static readonly TimeOnly Now = new(15, 32);

    private const string Thesis = "Tez raporu — 3. bölüm";

    public static void Seed(ISessionRepository sessions, IPlanRepository plans, TimeZoneInfo zone)
    {
        void Add(DateOnly day, string start, string end, string task, string tag)
        {
            var s = ZoneTime.ToInstant(day, TimeOnly.Parse(start), zone);
            var e = ZoneTime.ToInstant(day, TimeOnly.Parse(end), zone);
            sessions.Add(new TimeSession(Guid.NewGuid(), task, tag, s, e, e - s));
        }

        // Today: five sessions, 4:37 in total.
        Add(Today, "08:40", "10:10", Thesis, "yazım");
        Add(Today, "10:25", "11:20", "Kod inceleme", "yazılım");
        Add(Today, "11:30", "12:00", "Ekip toplantısı", "toplantı");
        Add(Today, "13:00", "13:45", "Literatür okuma", "araştırma");
        Add(Today, "14:00", "14:57", "Spor", "sağlık");

        // Previous week (the report scenario shows it), Mon-Fri.
        var monday = Today.AddDays(-7);
        (string Start, string End, string Task, string Tag)[][] week =
        [
            [("09:00", "11:30", Thesis, "yazım"), ("11:45", "12:15", "Ekip toplantısı", "toplantı"), ("13:30", "15:00", "Kod inceleme", "yazılım")],
            [("09:15", "10:45", "Literatür okuma", "araştırma"), ("11:00", "12:30", Thesis, "yazım"), ("14:00", "16:30", "Kod inceleme", "yazılım")],
            [("08:50", "11:50", Thesis, "yazım"), ("11:50", "12:20", "Ekip toplantısı", "toplantı"), ("17:00", "17:50", "Spor", "sağlık")],
            [("10:00", "11:20", "Literatür okuma", "araştırma"), ("13:30", "15:10", "Kod inceleme", "yazılım"), ("15:30", "17:00", Thesis, "yazım")],
            [("09:00", "10:30", Thesis, "yazım"), ("10:45", "11:15", "Ekip toplantısı", "toplantı"), ("16:00", "17:10", "Spor", "sağlık")],
        ];
        for (var i = 0; i < week.Length; i++)
            foreach (var (start, end, task, tag) in week[i])
                Add(monday.AddDays(i), start, end, task, tag);

        // Plan: one recurring weekday block, plus today's one-offs (the second and third overlap).
        plans.Add(new PlanBlock(Guid.NewGuid(), "Ekip toplantısı", "toplantı", new TimeOnly(11, 30), new TimeOnly(12, 0), null,
            new PlanRecurrence(new DateOnly(2026, 9, 28), null, WeekDays.Weekdays)));
        plans.Add(new PlanBlock(Guid.NewGuid(), Thesis, "yazım", new TimeOnly(9, 0), new TimeOnly(11, 0), Today, null));
        plans.Add(new PlanBlock(Guid.NewGuid(), "Kod inceleme", "yazılım", new TimeOnly(13, 30), new TimeOnly(15, 0), Today, null));
        plans.Add(new PlanBlock(Guid.NewGuid(), "Literatür okuma", "araştırma", new TimeOnly(14, 0), new TimeOnly(15, 0), Today, null));
        plans.Add(new PlanBlock(Guid.NewGuid(), "Spor", "sağlık", new TimeOnly(16, 0), new TimeOnly(17, 30), Today, null));
        // Last week's planned blocks so the report has a plan to compare against.
        for (var i = 0; i < 5; i++)
            plans.Add(new PlanBlock(Guid.NewGuid(), Thesis, "yazım", new TimeOnly(9, 0), new TimeOnly(11, 0), monday.AddDays(i), null));
    }
}
