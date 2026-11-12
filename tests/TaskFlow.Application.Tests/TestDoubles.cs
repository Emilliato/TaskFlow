using Microsoft.Extensions.Logging.Abstractions;
using TaskFlow.Application;
using TaskFlow.Domain;

namespace TaskFlow.Application.Tests;

// Hand-written FAKES: tiny, working, in-memory versions of the two repositories.
// They keep real state, so a test can ask "what ended up stored?" instead of
// "which methods were called?". Written once, shared by every test in the project.
internal class FakeTaskRepository(IEnumerable<TaskItem> seed) : ITaskRepository
{
    private readonly List<TaskItem> _tasks = seed.ToList();

    public IReadOnlyList<TaskItem> GetAll() => _tasks;
    public IReadOnlyList<TaskItem> Search(TaskFilter filter) => _tasks.Where(t => !t.IsArchived).ToList();
    public TaskItem? GetById(int id) => _tasks.FirstOrDefault(t => t.Id == id);
    public bool TitleExists(string title) => _tasks.Any(t => t.Title.Equals(title.Trim(), StringComparison.OrdinalIgnoreCase));
    public TaskItem Add(TaskItem input) { _tasks.Add(input); return input; }
    public TaskItem? Update(int id, TaskItem input) => throw new NotImplementedException();
    public bool Delete(int id) => _tasks.RemoveAll(t => t.Id == id) > 0;
}

internal class FakeCommentRepository : ICommentRepository
{
    public List<TaskComment> Stored { get; } = [];

    public TaskComment Add(TaskComment comment)
    {
        var saved = comment with { Id = Stored.Count + 1 };
        Stored.Add(saved);
        return saved;
    }

    public IReadOnlyDictionary<int, int> CountByTask(IReadOnlyCollection<int> taskIds) =>
        Stored.Where(c => taskIds.Contains(c.TaskId)).GroupBy(c => c.TaskId).ToDictionary(g => g.Key, g => g.Count());
}

internal static class Arrange
{
    // One place builds the service under test, so a new constructor parameter
    // is a one-line fix here, not a fix in every test.
    public static (TaskService Service, FakeCommentRepository Comments) Service(params TaskItem[] tasks)
    {
        var comments = new FakeCommentRepository();
        return (new TaskService(new FakeTaskRepository(tasks), comments, NullLogger<TaskService>.Instance), comments);
    }
}
