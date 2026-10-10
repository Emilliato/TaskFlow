using TaskFlow.Application;
using TaskFlow.Domain;

namespace TaskFlow.Application.Tests;

// No database. No web server. The "repositories" are dictionaries in memory,
// which is exactly what the interfaces in the Application layer make possible.
public class TaskServiceCommentTests
{
    [Fact]
    public void Comment_on_a_task_that_does_not_exist_is_rejected_and_nothing_is_stored()
    {
        var (service, _, comments) = Arrange(new TaskItem { Id = 1, Title = "Real task" });

        var result = service.AddComment(taskId: 999, author: "sam", body: "looks good");

        Assert.Equal(CommentOutcome.TaskNotFound, result.Outcome);
        Assert.Empty(comments.Stored);
    }

    [Fact]
    public void Comment_on_an_archived_task_is_rejected()
    {
        var (service, _, comments) = Arrange(new TaskItem { Id = 2, Title = "Old task", IsArchived = true });

        var result = service.AddComment(2, "sam", "still relevant?");

        Assert.Equal(CommentOutcome.TaskArchived, result.Outcome);
        Assert.Empty(comments.Stored);
    }

    [Fact]
    public void Comment_on_an_open_task_is_stored()
    {
        var (service, _, comments) = Arrange(new TaskItem { Id = 1, Title = "Real task" });

        var result = service.AddComment(1, "sam", "looks good");

        Assert.Equal(CommentOutcome.Added, result.Outcome);
        var stored = Assert.Single(comments.Stored);
        Assert.Equal(1, stored.TaskId);
        Assert.Equal("looks good", stored.Body);
    }

    private static (TaskService, FakeTaskRepository, FakeCommentRepository) Arrange(params TaskItem[] tasks)
    {
        var taskRepo = new FakeTaskRepository(tasks);
        var commentRepo = new FakeCommentRepository();
        return (new TaskService(taskRepo, commentRepo), taskRepo, commentRepo);
    }
}

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
