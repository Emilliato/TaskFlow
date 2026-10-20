namespace TaskFlow;

// The contract the endpoints depend on. They ask for an ITaskService and let
// the DI container decide which concrete class satisfies it — the endpoints
// never "new" anything up themselves.
public interface ITaskService
{
    // Changed in 3.2: IEnumerable<T> -> IQueryable<T>. IEnumerable promises
    // only "you can loop over this" — by the time a caller gets one back, any
    // filtering it does on top happens in memory, in this process, over
    // whatever the whole method already pulled back. IQueryable<T> is a
    // query that has not run yet: a caller composing .Where() onto it is
    // still building the SAME query, so it becomes part of the one SQL
    // statement the database eventually receives.
    //
    // Changed in 3.4: everything that TALKS to the database now returns a Task.
    // GetAll() does not: it only builds a description of a query, so there is
    // nothing to wait for yet. The wait starts when someone runs it, with
    // ToListAsync().
    IQueryable<TaskItem> GetAll();
    Task<TaskItem?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<TaskItem> AddAsync(TaskItem input, CancellationToken ct = default);
    Task<TaskItem?> UpdateAsync(int id, TaskItem input, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}
