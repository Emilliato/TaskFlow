using Microsoft.EntityFrameworkCore;

namespace TaskFlow;

// The same ITaskService contract from episode 2.2 — the endpoints do not
// change at all — but the store is now a real SQLite database instead of a
// List<TaskItem>. This class is the entire "swap the implementation" payoff of
// programming against an interface.
//
// Note what is NOT here: no connection strings, no SqlCommand, no reader loop,
// no hand-written SQL. That is the ORM's job. What IS here is LINQ over
// DbSet<TaskItem>, and a SaveChanges() to commit.
//
// Everything below is synchronous, deliberately: it keeps this episode about
// the ORM. Episode 3.4 is where all of this grows an Async suffix and a Task.
public class EfTaskService : ITaskService
{
    private readonly TaskFlowDbContext _db;

    public EfTaskService(TaskFlowDbContext db) => _db = db;

    // AsNoTracking: this data is read and thrown away, so EF does not need to
    // keep a copy to compare against later. Reads that never turn into writes
    // should say so.
    public IEnumerable<TaskItem> GetAll() =>
        _db.Tasks.AsNoTracking().OrderBy(t => t.Id).ToList();

    public TaskItem? GetById(int id) =>
        _db.Tasks.AsNoTracking().FirstOrDefault(t => t.Id == id);

    public TaskItem Add(TaskItem input)
    {
        // No Id is set here. The column is INTEGER PRIMARY KEY AUTOINCREMENT's
        // cousin in SQLite, and EF reads the generated value back into the
        // entity as part of the INSERT.
        var created = input with { CreatedAt = DateTimeOffset.UtcNow };

        _db.Tasks.Add(created);   // change tracker: state = Added
        _db.SaveChanges();        // one INSERT, inside a transaction
        return created;
    }

    public TaskItem? Update(int id, TaskItem input)
    {
        // Tracked on purpose this time: EF has to hold the ORIGINAL values to
        // work out which columns actually changed.
        var existing = _db.Tasks.FirstOrDefault(t => t.Id == id);
        if (existing is null) return null;

        var updated = input with { Id = id, CreatedAt = existing.CreatedAt };

        // CompletedAt is server-owned, like CreatedAt: it is stamped when the
        // task flips to done, and cleared if it is reopened.
        updated = updated with
        {
            CompletedAt = updated.Done
                ? existing.CompletedAt ?? DateTimeOffset.UtcNow
                : null,
        };

        // Copy the new values onto the tracked entity. The change tracker
        // compares them with the originals and writes an UPDATE containing
        // only the columns whose values are genuinely different.
        _db.Entry(existing).CurrentValues.SetValues(updated);
        _db.SaveChanges();
        return existing;
    }

    public bool Delete(int id)
    {
        var existing = _db.Tasks.FirstOrDefault(t => t.Id == id);
        if (existing is null) return false;

        _db.Tasks.Remove(existing);
        _db.SaveChanges();
        return true;
    }
}
