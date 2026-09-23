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
    IQueryable<TaskItem> GetAll();
    TaskItem? GetById(int id);
    TaskItem Add(TaskItem input);
    TaskItem? Update(int id, TaskItem input);
    bool Delete(int id);
}
