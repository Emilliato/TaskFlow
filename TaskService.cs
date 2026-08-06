namespace TaskFlow;

// The concrete implementation. It owns the in-memory store that used to live
// inline in Program.cs. Registered as a Singleton, so the same instance — and
// therefore the same list — is shared by every request. That single shared
// instance is exactly why a task you POST is still there on the next GET.
public class TaskService : ITaskService
{
    private readonly List<TaskItem> _tasks =
    [
        new() { Id = 1, Title = "Set up the Git repo", Done = true, Priority = "normal",
                CreatedAt = new DateTimeOffset(2026, 7, 12, 9, 0, 0, TimeSpan.Zero),
                CreatedBy = "platform-team", InternalNotes = "billing code OPS-114" },
        new() { Id = 2, Title = "Open the first pull request", Done = true, Priority = "normal",
                CreatedAt = new DateTimeOffset(2026, 7, 12, 9, 5, 0, TimeSpan.Zero),
                CreatedBy = "platform-team", InternalNotes = "billing code OPS-114" },
        new() { Id = 3, Title = "Build the REST API", Done = false, Priority = "high",
                DueDate = new DateOnly(2026, 12, 1),
                CreatedAt = new DateTimeOffset(2026, 7, 12, 9, 30, 0, TimeSpan.Zero),
                CreatedBy = "platform-team", InternalNotes = "internal: blocked on the DTO refactor" },
    ];
    private int _nextId = 4;

    public IEnumerable<TaskItem> GetAll() => _tasks;

    public TaskItem? GetById(int id) => _tasks.FirstOrDefault(t => t.Id == id);

    public TaskItem Add(TaskItem input)
    {
        var created = input with { Id = _nextId++, CreatedAt = DateTimeOffset.UtcNow };
        _tasks.Add(created);
        return created;
    }

    public TaskItem? Update(int id, TaskItem input)
    {
        var index = _tasks.FindIndex(t => t.Id == id);
        if (index < 0) return null;
        var updated = input with { Id = id, CreatedAt = _tasks[index].CreatedAt };
        _tasks[index] = updated;
        return updated;
    }

    public bool Delete(int id) => _tasks.RemoveAll(t => t.Id == id) > 0;
}
