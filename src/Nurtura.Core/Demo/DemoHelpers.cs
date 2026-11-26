namespace Nurtura.Core.Demo;

internal static class DemoTime
{
    // A Poland wall-clock time as UTC. A time skipped when the clocks go forward moves one hour later.
    public static DateTime Utc(DateOnly date, int minuteOfDay)
    {
        var time = new TimeOnly(minuteOfDay / 60, minuteOfDay % 60);
        if (!PolandTime.TryToUtc(date, time, out var utc))
        {
            PolandTime.TryToUtc(date, time.AddHours(1), out utc);
        }

        return utc;
    }

    public static DateTime Utc(DateOnly date, int hour, int minute) => Utc(date, hour * 60 + minute);

    public static DateOnly LocalDate(DateTime utc) => DateOnly.FromDateTime(PolandTime.ToLocal(utc));

    public static int LocalHour(DateTime utc) => PolandTime.ToLocal(utc).Hour;
}

internal static class DemoRandom
{
    // A whole number from `from` to `to`, both included.
    public static int Pick(this Random rng, int from, int to) => rng.Next(from, to + 1);

    public static double Between(this Random rng, double from, double to) => from + (to - from) * rng.NextDouble();

    public static bool Chance(this Random rng, double probability) => rng.NextDouble() < probability;

    public static T PickOne<T>(this Random rng, IReadOnlyList<T> items) => items[rng.Next(items.Count)];

    // `count` different items, in random order.
    public static HashSet<T> Sample<T>(this Random rng, IReadOnlyList<T> items, int count) =>
        items.OrderBy(_ => rng.Next()).Take(count).ToHashSet();
}

// Hands out texts in a shuffled order, starting over when all were used, so the same note does not come up
// again and again.
internal sealed class NotePool(IReadOnlyList<string> texts, Random rng)
{
    private readonly Queue<string> _queue = new();

    public string Next()
    {
        if (_queue.Count == 0)
        {
            foreach (var text in texts.OrderBy(_ => rng.Next()))
            {
                _queue.Enqueue(text);
            }
        }

        return _queue.Dequeue();
    }
}
