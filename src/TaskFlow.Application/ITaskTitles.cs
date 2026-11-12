namespace TaskFlow.Application;

// A narrow role: the one question the create-task rule needs answered. A caller that
// only asks this should not have to depend on everything else TaskService can do.
public interface ITaskTitles
{
    bool TitleExists(string title);
}
