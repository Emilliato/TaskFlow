namespace TaskFlow;

// The ENTITY: TaskFlow's own model of a task.
//
// An entity is shaped by what the SERVER needs. It therefore carries three
// kinds of field, and only the first kind is any of a client's business:
//
//   * client data      Title, Done, DueDate, Priority
//   * server-assigned  Id, CreatedAt, CreatedBy   — set here, never taken
//                                                   from a request body
//   * internal-only    InternalNotes, IsArchived  — must never be settable,
//                                                   and InternalNotes must
//                                                   never even be readable
//
// Bind an endpoint straight to this type and every one of those fields becomes
// part of your public API by accident. That is the whole reason DTOs exist.
public record TaskItem
{
    public int Id { get; init; }
    public string Title { get; init; } = "";
    public bool Done { get; init; }
    public DateOnly? DueDate { get; init; }
    public string Priority { get; init; } = "normal";

    // ---- server-owned ----
    public DateTimeOffset CreatedAt { get; init; }
    public string CreatedBy { get; init; } = "system";

    // ---- internal-only ----
    public string InternalNotes { get; init; } = "";
    public bool IsArchived { get; init; }
}
