namespace TaskFlow;

// The contract the endpoints depend on. They ask for an ITaskService and let
// the DI container decide which concrete class satisfies it — the endpoints
// never "new" anything up themselves.
public interface ITaskService
{
    IEnumerable<TaskItem> GetAll();
    TaskItem? GetById(int id);
    TaskItem Add(TaskItem input);
    TaskItem? Update(int id, TaskItem input);
    bool Delete(int id);
}
