using Microsoft.Extensions.Logging;
using TaskFlow.Domain;

namespace TaskFlow.Application;

public enum CommentOutcome { Added, TaskNotFound, TaskArchived }

public record AddCommentResult(CommentOutcome Outcome, TaskComment? Comment = null);

public record TaskStats(int Total, int Done, int Open);

public record TaskCommentSummary(int Id, string Title, int CommentCount);

// ======================= THE APPLICATION LAYER =============================
// The use cases: what TaskFlow DOES. It orchestrates the domain and the
// repositories, and it is the only place that decides "may this happen?".
//
// It depends on two interfaces and on the Domain. It has never heard of HTTP
// status codes, JSON, DbContext or SQLite - so it can be constructed in a
// unit test with two fakes and run in milliseconds.
// ===========================================================================
public class TaskService(ITaskRepository tasks, ICommentRepository comments, ILogger<TaskService> logger)
{
    // Who is told when somebody comments: the person or team that created the task.
    private static readonly Dictionary<string, string> OwnerEmails = new()
    {
        ["system"] = "ops@taskflow.example",
        ["platform-team"] = "platform@taskflow.example",
    };

    public IReadOnlyList<TaskItem> List(TaskFilter filter) => tasks.Search(filter);

    public TaskItem? Get(int id) => tasks.GetById(id);

    public bool TitleExists(string title) => tasks.TitleExists(title);

    public TaskItem Create(TaskItem input) => tasks.Add(input);

    public TaskItem? Update(int id, TaskItem input) => tasks.Update(id, input);

    public bool Delete(int id) => tasks.Delete(id);

    public TaskStats GetStats()
    {
        var all = tasks.GetAll();
        return new TaskStats(all.Count, all.Count(t => t.Done), all.Count(t => !t.Done));
    }

    // THE rule that was missing. Before this layer existed, the endpoint wrote
    // the comment straight to the database and nothing ever asked whether the
    // task was real. Now the answer has exactly one home.
    public AddCommentResult AddComment(int taskId, string author, string body)
    {
        // One span around the use case: it shows up in a trace as a child of the HTTP request.
        using var activity = TaskFlowTelemetry.Source.StartActivity("AddComment");
        activity?.SetTag("task.id", taskId);

        logger.LogInformation("Adding a comment to task {TaskId} by {Author}", taskId, author);
        var task = tasks.GetById(taskId);
        if (task is null)
        {
            logger.LogWarning("Comment rejected: task {TaskId} does not exist", taskId);
            return new AddCommentResult(CommentOutcome.TaskNotFound);
        }
        if (!task.AcceptsComments)
        {
            logger.LogWarning("Comment rejected: task {TaskId} is archived", taskId);
            return new AddCommentResult(CommentOutcome.TaskArchived);
        }

        // Fall back to the ops mailbox for owners we do not know about.
        var recipient = OwnerEmails.TryGetValue(task.CreatedBy, out var email) ? email : "ops@taskflow.example";
        logger.LogDebug("Comment on task {TaskId} will notify the owner {CreatedBy}", taskId, task.CreatedBy);
        var saved = comments.Add(new TaskComment
        {
            TaskId = taskId,
            Author = author,
            Body = body,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        TaskFlowTelemetry.CommentsAdded.Add(1, new KeyValuePair<string, object?>("task.priority", task.Priority));
        logger.LogInformation("Added comment {CommentId} to task {TaskId}", saved.Id, taskId);
        return new AddCommentResult(CommentOutcome.Added, saved);
    }

    // The N+1 fix from 3.2, now a use case instead of endpoint code.
    public IReadOnlyList<TaskCommentSummary> GetCommentSummary()
    {
        var all = tasks.GetAll();
        var counts = comments.CountByTask(all.Select(t => t.Id).ToList());
        return all.Select(t => new TaskCommentSummary(t.Id, t.Title, counts.GetValueOrDefault(t.Id))).ToList();
    }
}
