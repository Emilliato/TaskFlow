using Microsoft.EntityFrameworkCore;
using TaskFlow.Application;
using TaskFlow.Domain;

namespace TaskFlow.Infrastructure;

public class EfCommentRepository(TaskFlowDbContext db) : ICommentRepository
{
    public TaskComment Add(TaskComment comment)
    {
        db.Comments.Add(comment);
        db.SaveChanges();
        return comment;
    }

    // The 3.2 fix, unchanged: one grouped query for every count.
    public IReadOnlyDictionary<int, int> CountByTask(IReadOnlyCollection<int> taskIds) =>
        db.Comments.AsNoTracking()
            .Where(c => taskIds.Contains(c.TaskId))
            .GroupBy(c => c.TaskId)
            .Select(g => new { TaskId = g.Key, Count = g.Count() })
            .ToDictionary(g => g.TaskId, g => g.Count);
}
