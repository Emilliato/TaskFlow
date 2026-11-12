namespace TaskFlow.Application;

// The use case needs ONE fact from the outside world: whom to tell when somebody
// comments on a task. It states that need as an interface, here, in its own layer.
// How the answer is found (a settings file, a database, a directory service) is
// somebody else's job, and the use case never learns which.
public interface IOwnerDirectory
{
    /// <summary>The mailbox to tell about a comment on a task created by this owner.</summary>
    string EmailFor(string createdBy);
}
