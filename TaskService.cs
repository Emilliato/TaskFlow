namespace TaskFlow;

// The concrete implementation. It owns the in-memory store that used to live
// inline in Program.cs. Registered as a Singleton, so the same instance — and
// therefore the same list — is shared by every request. That single shared
// instance is exactly why a task you POST is still there on the next GET.
public class TaskService : ITaskService
{
    private readonly List<TaskItem> _tasks =
    [
        new(1, "Set up the Git repo", true),
        new(2, "Open the first pull request", true),
        new(3, "Build the REST API", false),
    ];
    private int _nextId = 4;

    public IEnumerable<TaskItem> GetAll() => _tasks;

    public TaskItem? GetById(int id) => _tasks.FirstOrDefault(t => t.Id == id);

    public TaskItem Add(TaskItem input)
    {
        var created = input with { Id = _nextId++ };
        _tasks.Add(created);
        return created;
    }

    public TaskItem? Update(int id, TaskItem input)
    {
        var index = _tasks.FindIndex(t => t.Id == id);
        if (index < 0) return null;
        var updated = input with { Id = id };
        _tasks[index] = updated;
        return updated;
    }

    public bool Delete(int id) => _tasks.RemoveAll(t => t.Id == id) > 0;
}
