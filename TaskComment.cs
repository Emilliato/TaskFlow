namespace TaskFlow;

// A second real table, added for episode 3.2's N+1 demo: every task can have
// zero or more comments. Deliberately NO navigation property on TaskItem —
// the N+1 trap does not need Include() or a collection nav property to
// happen. It happens with nothing more than two DbSets and a loop, which is
// exactly how most people meet it the first time.
public record TaskComment
{
    public int Id { get; init; }
    public int TaskId { get; init; }
    public string Author { get; init; } = "";
    public string Body { get; init; } = "";
    public DateTimeOffset CreatedAt { get; init; }
}
