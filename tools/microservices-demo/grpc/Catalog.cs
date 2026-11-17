using Google.Protobuf.WellKnownTypes;

namespace GrpcDemo;

// The same made-up tasks, served over both protocols.
public record TaskRow(int Id, string Title, bool Done, string Priority, string CreatedBy, DateTimeOffset CreatedAt);

public static class Catalog
{
    private static readonly string[] Priorities = ["low", "normal", "high"];
    private static readonly DateTimeOffset Start = new(2026, 7, 12, 9, 0, 0, TimeSpan.Zero);

    public static List<TaskRow> Make(int count) =>
        Enumerable.Range(1, count)
            .Select(i => new TaskRow(i, $"Review pull request number {i}", i % 3 == 0,
                Priorities[i % 3], "platform-team", Start.AddMinutes(i)))
            .ToList();

    public static TaskReply ToReply(this TaskRow row) => new()
    {
        Id = row.Id, Title = row.Title, Done = row.Done, Priority = row.Priority,
        CreatedBy = row.CreatedBy, CreatedAt = Timestamp.FromDateTimeOffset(row.CreatedAt),
    };
}
