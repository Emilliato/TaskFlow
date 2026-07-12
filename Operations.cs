namespace TaskFlow;

// A tiny marker service whose whole job is to make service lifetimes VISIBLE.
// Each instance stamps itself with a fresh Guid at construction time, so if two
// things hold "the same" service, they show the same id — and if the container
// handed out two different instances, the ids differ. We register ONE class
// under three interfaces, each with a different lifetime, and let the ids talk.
public interface IOperation
{
    Guid Id { get; }
}

public interface ITransientOperation : IOperation;  // new instance every time it's resolved
public interface IScopedOperation : IOperation;     // one instance per request (per scope)
public interface ISingletonOperation : IOperation;  // one instance for the whole app

public class Operation : ITransientOperation, IScopedOperation, ISingletonOperation
{
    // Assigned once, when the container constructs this instance.
    public Guid Id { get; } = Guid.NewGuid();
}

// A service that receives all three operations through its CONSTRUCTOR — the
// container fills these parameters in. This is constructor injection: the class
// declares what it needs and is handed it, instead of new-ing anything itself.
public class OperationLogger(
    ITransientOperation transient,
    IScopedOperation scoped,
    ISingletonOperation singleton)
{
    public ITransientOperation Transient { get; } = transient;
    public IScopedOperation Scoped { get; } = scoped;
    public ISingletonOperation Singleton { get; } = singleton;
}
