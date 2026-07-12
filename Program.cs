// TaskFlow API — the course capstone, now a real ASP.NET Core Web API.
using TaskFlow;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// In-memory task store. We move this behind a DI-registered service in 2.2;
// for now it lives right here so the REST shape stays front and centre.
var tasks = new List<TaskItem>
{
    new(1, "Set up the Git repo", true),
    new(2, "Open the first pull request", true),
    new(3, "Build the REST API", false),
};
var nextId = 4;

// GET /tasks — read the whole collection of task resources. 200 OK.
app.MapGet("/tasks", () => tasks);

// GET /tasks/{id} — read one task resource, or 404 if it isn't there.
app.MapGet("/tasks/{id:int}", (int id) =>
{
    var task = tasks.FirstOrDefault(t => t.Id == id);
    return task is null ? Results.NotFound() : Results.Ok(task);
});

// POST /tasks — create a new task. 201 Created + a Location header to it.
app.MapPost("/tasks", (TaskItem input) =>
{
    var created = input with { Id = nextId++ };
    tasks.Add(created);
    return Results.Created($"/tasks/{created.Id}", created);
});

// PUT /tasks/{id} — replace a task's state. 200 OK, or 404 if it's missing.
app.MapPut("/tasks/{id:int}", (int id, TaskItem input) =>
{
    var index = tasks.FindIndex(t => t.Id == id);
    if (index < 0) return Results.NotFound();
    var updated = input with { Id = id };
    tasks[index] = updated;
    return Results.Ok(updated);
});

// DELETE /tasks/{id} — remove a task. 204 No Content, or 404 if it's missing.
app.MapDelete("/tasks/{id:int}", (int id) =>
{
    var removed = tasks.RemoveAll(t => t.Id == id) > 0;
    return removed ? Results.NoContent() : Results.NotFound();
});

app.Run();
