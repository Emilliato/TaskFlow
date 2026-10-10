using TaskFlow.Application;
using TaskFlow.Domain;

namespace TaskFlow.Infrastructure;

// A second implementation of the SAME two interfaces, with no database at all.
// Nothing above Infrastructure changes to use it: the endpoints, TaskService
// and the validator all keep asking ITaskRepository the same questions.
// That is what "the application does not know what is behind the repository"
// means in practice - and it is a handy store for demos and local development.
public class InMemoryTaskRepository : ITaskRepository
{
    private static readonly DateTimeOffset Seeded = new(2026, 7, 12, 9, 0, 0, TimeSpan.Zero);
    private readonly object _gate = new();
    private readonly List<TaskItem> _tasks =
    [
        new() { Id = 1, Title = "Set up the Git repo", Done = true, CreatedAt = Seeded, CreatedBy = "platform-team" },
        new() { Id = 2, Title = "Open the first pull request", Done = true, CreatedAt = Seeded, CreatedBy = "platform-team" },
        new() { Id = 3, Title = "Build the REST API", Priority = "high", CreatedAt = Seeded, CreatedBy = "platform-team" },
    ];
    private int _nextId = 4;

    public IReadOnlyList<TaskItem> GetAll()
    {
        lock (_gate) return _tasks.OrderBy(t => t.Id).ToList();
    }

    public IReadOnlyList<TaskItem> Search(TaskFilter filter)
    {
        lock (_gate)
        {
            var query = _tasks.Where(t => !t.IsArchived);
            if (filter.Done is not null) query = query.Where(t => t.Done == filter.Done);
            if (!string.IsNullOrWhiteSpace(filter.Search))
                query = query.Where(t => t.Title.Contains(filter.Search, StringComparison.OrdinalIgnoreCase));

            return query.OrderBy(t => t.Id)
                        .Skip((filter.Page - 1) * filter.PageSize)
                        .Take(filter.PageSize)
                        .ToList();
        }
    }

    public TaskItem? GetById(int id)
    {
        lock (_gate) return _tasks.FirstOrDefault(t => t.Id == id);
    }

    public bool TitleExists(string title)
    {
        lock (_gate) return _tasks.Any(t => t.Title.Equals(title.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public TaskItem Add(TaskItem input)
    {
        lock (_gate)
        {
            var created = input with { Id = _nextId++, CreatedAt = DateTimeOffset.UtcNow };
            _tasks.Add(created);
            return created;
        }
    }

    public TaskItem? Update(int id, TaskItem input)
    {
        lock (_gate)
        {
            var index = _tasks.FindIndex(t => t.Id == id);
            if (index < 0) return null;

            var existing = _tasks[index];
            var updated = input with { Id = id, CreatedAt = existing.CreatedAt };
            _tasks[index] = updated with
            {
                CompletedAt = updated.Done ? existing.CompletedAt ?? DateTimeOffset.UtcNow : null,
            };
            return _tasks[index];
        }
    }

    public bool Delete(int id)
    {
        lock (_gate) return _tasks.RemoveAll(t => t.Id == id) > 0;
    }
}

public class InMemoryCommentRepository : ICommentRepository
{
    private readonly object _gate = new();
    private readonly List<TaskComment> _comments = [];

    public TaskComment Add(TaskComment comment)
    {
        lock (_gate)
        {
            var saved = comment with { Id = _comments.Count + 1 };
            _comments.Add(saved);
            return saved;
        }
    }

    public IReadOnlyDictionary<int, int> CountByTask(IReadOnlyCollection<int> taskIds)
    {
        lock (_gate)
        {
            return _comments.Where(c => taskIds.Contains(c.TaskId))
                            .GroupBy(c => c.TaskId)
                            .ToDictionary(g => g.Key, g => g.Count());
        }
    }
}
