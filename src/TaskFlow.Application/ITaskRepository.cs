using TaskFlow.Domain;

namespace TaskFlow.Application;

// ======================= THE REPOSITORY PATTERN ============================
// A repository is a collection-shaped front door to stored data: the
// application asks it for TaskItems the way it would ask a List<TaskItem>,
// and has no idea whether a SQLite file, SQL Server or a dictionary sits
// behind it.
//
// The INTERFACE lives here, in the Application layer, because the Application
// layer is the one that needs it. The IMPLEMENTATION lives in Infrastructure.
// That inversion is the whole trick: Infrastructure depends on Application,
// never the other way round.
//
// Note what it does NOT return: IQueryable. Episode 3.2 needed IQueryable so
// a filter could compose into the SQL. Here the filter travels as a plain
// TaskFilter value and the implementation decides how to apply it, so no
// caller can ever hand the database an expression it cannot translate.
// ===========================================================================
public interface ITaskRepository
{
    IReadOnlyList<TaskItem> GetAll();
    IReadOnlyList<TaskItem> Search(TaskFilter filter);
    TaskItem? GetById(int id);
    bool TitleExists(string title);
    TaskItem Add(TaskItem input);
    TaskItem? Update(int id, TaskItem input);
    bool Delete(int id);
}

public interface ICommentRepository
{
    TaskComment Add(TaskComment comment);

    // One grouped query's worth of answer: taskId -> number of comments.
    IReadOnlyDictionary<int, int> CountByTask(IReadOnlyCollection<int> taskIds);
}

/// <summary>What a caller may filter a task list by. A value, not an expression.</summary>
public record TaskFilter(bool? Done, string? Search, int Page = 1, int PageSize = 20);
