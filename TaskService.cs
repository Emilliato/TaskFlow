namespace TaskFlow;

// The IN-MEMORY implementation, from episode 2.2. As of 3.1 it is no longer
// registered with the container — EfTaskService is — but it is deliberately
// kept: an implementation of ITaskService that needs no database is exactly
// what unit tests will want in week 4.
//
// It also remains the clearest statement of the problem EF Core solves. The
// "database" below is a field. When the process exits, so does the data.
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

    // AsQueryable() over a List<T> is still LINQ-to-Objects underneath — there
    // is no SQL to translate to here — but the interface changed in 3.2, so
    // every implementation of it has to satisfy the same contract. The
    // compiler is what makes sure this line did not get missed.
    public IQueryable<TaskItem> GetAll() => _tasks.AsQueryable();

    public Task<TaskItem?> GetByIdAsync(int id, CancellationToken ct = default) =>
        Task.FromResult(_tasks.FirstOrDefault(t => t.Id == id));

    public Task<TaskItem> AddAsync(TaskItem input, CancellationToken ct = default)
    {
        var created = input with { Id = _nextId++, CreatedAt = DateTimeOffset.UtcNow };
        _tasks.Add(created);
        return Task.FromResult(created);
    }

    public Task<TaskItem?> UpdateAsync(int id, TaskItem input, CancellationToken ct = default)
    {
        var index = _tasks.FindIndex(t => t.Id == id);
        if (index < 0) return Task.FromResult<TaskItem?>(null);
        var updated = input with { Id = id, CreatedAt = _tasks[index].CreatedAt };
        _tasks[index] = updated;
        return Task.FromResult<TaskItem?>(updated);
    }

    public Task<bool> DeleteAsync(int id, CancellationToken ct = default) =>
        Task.FromResult(_tasks.RemoveAll(t => t.Id == id) > 0);
}
