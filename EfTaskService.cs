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
// Changed in 3.4: every method that touches the database is async. Each one
// grew an Async suffix, a Task<T> return type, and an await.
public class EfTaskService : ITaskService
{
    private readonly TaskFlowDbContext _db;

    public EfTaskService(TaskFlowDbContext db) => _db = db;

    // AsNoTracking: this data is read and thrown away, so EF does not need to
    // keep a copy to compare against later. Reads that never turn into writes
    // should say so.
    //
    // Changed in 3.2: no more .ToList() here. That call was the whole bug —
    // it ran the query immediately, inside this method, before a caller ever
    // got a chance to add a .Where(). Returning the IQueryable itself instead
    // means the expression tree stays open for composition: whatever a
    // caller filters on top gets folded into ONE SELECT, not applied in
    // memory after the fact.
    public IQueryable<TaskItem> GetAll() =>
        _db.Tasks.AsNoTracking().OrderBy(t => t.Id);

    public Task<TaskItem?> GetByIdAsync(int id, CancellationToken ct = default) =>
        _db.Tasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<TaskItem> AddAsync(TaskItem input, CancellationToken ct = default)
    {
        // No Id is set here. The column is INTEGER PRIMARY KEY AUTOINCREMENT's
        // cousin in SQLite, and EF reads the generated value back into the
        // entity as part of the INSERT.
        var created = input with { CreatedAt = DateTimeOffset.UtcNow };

        _db.Tasks.Add(created);   // change tracker: state = Added
        await _db.SaveChangesAsync(ct);   // one INSERT, inside a transaction
        return created;
    }

    public async Task<TaskItem?> UpdateAsync(int id, TaskItem input, CancellationToken ct = default)
    {
        // Tracked on purpose this time: EF has to hold the ORIGINAL values to
        // work out which columns actually changed.
        var existing = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == id, ct);
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
        await _db.SaveChangesAsync(ct);
        return existing;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var existing = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (existing is null) return false;

        _db.Tasks.Remove(existing);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
