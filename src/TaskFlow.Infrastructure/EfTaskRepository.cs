using Microsoft.EntityFrameworkCore;
using TaskFlow.Application;
using TaskFlow.Domain;

namespace TaskFlow.Infrastructure;

// The same EF Core code that used to be EfTaskService - moved, not rewritten.
// What changed is who is allowed to see it: only the host that wires it up.
// This class is also where a SQL-translation detail finally belongs: the
// EF.Functions.Like from episode 3.2 is an Infrastructure fact, not something
// an endpoint should know about.
public class EfTaskRepository(TaskFlowDbContext db) : ITaskRepository
{
    public IReadOnlyList<TaskItem> GetAll() =>
        db.Tasks.AsNoTracking().OrderBy(t => t.Id).ToList();

    public IReadOnlyList<TaskItem> Search(TaskFilter filter)
    {
        var query = db.Tasks.AsNoTracking().Where(t => !t.IsArchived);

        if (filter.Done is not null)
            query = query.Where(t => t.Done == filter.Done);

        if (!string.IsNullOrWhiteSpace(filter.Search))
            query = query.Where(t => EF.Functions.Like(t.Title, $"%{filter.Search}%"));

        return query.OrderBy(t => t.Id)
                    .Skip((filter.Page - 1) * filter.PageSize)
                    .Take(filter.PageSize)
                    .ToList();
    }

    public TaskItem? GetById(int id) =>
        db.Tasks.AsNoTracking().FirstOrDefault(t => t.Id == id);

    public bool TitleExists(string title)
    {
        var wanted = title.Trim().ToLower();
        return db.Tasks.Any(t => t.Title.ToLower() == wanted);
    }

    public TaskItem Add(TaskItem input)
    {
        var created = input with { CreatedAt = DateTimeOffset.UtcNow };
        db.Tasks.Add(created);
        db.SaveChanges();
        return created;
    }

    public TaskItem? Update(int id, TaskItem input)
    {
        var existing = db.Tasks.FirstOrDefault(t => t.Id == id);
        if (existing is null) return null;

        var updated = input with { Id = id, CreatedAt = existing.CreatedAt };
        updated = updated with
        {
            CompletedAt = updated.Done ? existing.CompletedAt ?? DateTimeOffset.UtcNow : null,
        };

        db.Entry(existing).CurrentValues.SetValues(updated);
        db.SaveChanges();
        return existing;
    }

    public bool Delete(int id)
    {
        var existing = db.Tasks.FirstOrDefault(t => t.Id == id);
        if (existing is null) return false;

        db.Tasks.Remove(existing);
        db.SaveChanges();
        return true;
    }
}
