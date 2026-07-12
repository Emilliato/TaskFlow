// TaskFlow API — endpoints now depend on DI-registered services, not on
// objects they "new" up themselves. The store moved out of Program.cs (2.1)
// and behind ITaskService (2.2).
using TaskFlow;

var builder = WebApplication.CreateBuilder(args);

// ---- Register services with the DI container (the IoC container) ----
// The task store: ONE shared instance for the whole app, so its in-memory list
// survives from one request to the next. An in-memory store must be a Singleton.
builder.Services.AddSingleton<ITaskService, TaskService>();

// The lifetime demo: the SAME Operation class, registered three ways so we can
// watch how long each instance actually lives.
builder.Services.AddTransient<ITransientOperation, Operation>(); // new every resolution
builder.Services.AddScoped<IScopedOperation, Operation>();       // one per request
builder.Services.AddSingleton<ISingletonOperation, Operation>(); // one forever

// A consumer that constructor-injects all three (registered per-request).
builder.Services.AddScoped<OperationLogger>();

var app = builder.Build();

// ---- Endpoints: they ASK for ITaskService; the container hands it over ----
// No "var tasks = new List<...>" and no "new TaskService()" anywhere here.

// GET /tasks — read the whole collection.
app.MapGet("/tasks", (ITaskService svc) => svc.GetAll());

// GET /tasks/{id} — one task, or 404.
app.MapGet("/tasks/{id:int}", (int id, ITaskService svc) =>
    svc.GetById(id) is { } task ? Results.Ok(task) : Results.NotFound());

// POST /tasks — create. 201 + Location.
app.MapPost("/tasks", (TaskItem input, ITaskService svc) =>
{
    var created = svc.Add(input);
    return Results.Created($"/tasks/{created.Id}", created);
});

// PUT /tasks/{id} — replace. 200, or 404.
app.MapPut("/tasks/{id:int}", (int id, TaskItem input, ITaskService svc) =>
    svc.Update(id, input) is { } updated ? Results.Ok(updated) : Results.NotFound());

// DELETE /tasks/{id} — remove. 204, or 404.
app.MapDelete("/tasks/{id:int}", (int id, ITaskService svc) =>
    svc.Delete(id) ? Results.NoContent() : Results.NotFound());

// GET /diag — the lifetime probe. It asks the container for each operation
// TWICE (once directly, once via the OperationLogger service) inside a SINGLE
// request, and reports the first 8 chars of each instance's id. Compare the
// two columns within a request, then compare across requests, and each
// lifetime's rule shows itself:
//   transient → different every single time
//   scoped    → same within a request, new across requests
//   singleton → the same id everywhere, always
app.MapGet("/diag", (
    ITransientOperation transient,
    IScopedOperation scoped,
    ISingletonOperation singleton,
    OperationLogger logger) => Results.Ok(new
{
    endpoint = new
    {
        transient = Short(transient.Id),
        scoped = Short(scoped.Id),
        singleton = Short(singleton.Id),
    },
    service = new
    {
        transient = Short(logger.Transient.Id),
        scoped = Short(logger.Scoped.Id),
        singleton = Short(logger.Singleton.Id),
    },
}));

app.Run();

static string Short(Guid id) => id.ToString()[..8];
