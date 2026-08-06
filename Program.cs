// TaskFlow API — the same endpoints as before, now sitting at the end of an
// explicit request pipeline: Kestrel -> exception handling -> correlation ->
// timing -> routing -> gate -> endpoint, and back out again.
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TaskFlow;

var builder = WebApplication.CreateBuilder(args);

// One log line per event, so the pipeline reads top-to-bottom in the console.
builder.Logging.AddSimpleConsole(options =>
{
    options.SingleLine = true;
    options.TimestampFormat = "HH:mm:ss.fff ";
});

// ---- Register services with the DI container (episode 2.2) ----
builder.Services.AddSingleton<ITaskService, TaskService>();

builder.Services.AddTransient<ITransientOperation, Operation>(); // new every resolution
builder.Services.AddScoped<IScopedOperation, Operation>();       // one per request
builder.Services.AddSingleton<ISingletonOperation, Operation>(); // one forever
builder.Services.AddScoped<OperationLogger>();

var app = builder.Build();

// =====================  THE REQUEST PIPELINE  ==============================
// Registration ORDER is the API here. Each Use* call wraps everything that is
// registered after it, so the first middleware is the outermost one: first to
// see the request, last to see the response.
// ===========================================================================

// 1. Exception handling goes FIRST, so it wraps every middleware below it.
if (app.Environment.IsDevelopment())
{
    // Development only: the full exception and stack trace go to the CLIENT.
    app.UseDeveloperExceptionPage();
}
else
{
    // Production: log the detail, return one predictable, safe shape (RFC 7807).
    app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
    {
        var error = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        var correlationId = context.Items[CorrelationIdMiddleware.HeaderName] as string
                            ?? context.TraceIdentifier;

        app.Logger.LogError(error, "Unhandled exception for {Path} (correlation {Id})",
            context.Request.Path, correlationId);

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred.",
                Detail = "The failure has been logged. Quote the correlation id if you contact support.",
                Instance = context.Request.Path,
                Extensions = { ["correlationId"] = correlationId },
            },
            options: null,
            contentType: "application/problem+json");
    }));
}

// 2. Correlation id: every request gets an id, on the way in and on the way out.
app.UseMiddleware<CorrelationIdMiddleware>();

// 3. Timing: measures everything registered BELOW it (and nothing above it).
app.UseMiddleware<RequestTimingMiddleware>();

// 4. Routing: matches the URL against the endpoint table. Above this line
//    nobody knows which endpoint will run; below it, everybody does.
app.UseRouting();

// 5. A deliberate failure raised from MIDDLEWARE (not from an endpoint), used
//    to prove which middleware can and cannot catch it. Same spirit as /diag.
app.Use(async (context, next) =>
{
    if (context.Request.Path == "/diag/boom-middleware")
        throw new InvalidOperationException("Simulated failure raised from middleware.");

    await next(context);
});

// 6. Proof that routing has already CHOSEN the endpoint — it just hasn't run it.
app.Use(async (context, next) =>
{
    app.Logger.LogInformation("     ROUTING   selected: {Endpoint}",
        context.GetEndpoint()?.DisplayName ?? "(no endpoint matched -> 404)");

    await next(context);
});

// 7. The gate: after routing (so it can see the endpoint), before the endpoint runs.
app.UseMiddleware<ApiKeyGateMiddleware>();

// =====================  ENDPOINTS (terminal middleware)  ===================
// The endpoint is where the pipeline stops going forward and starts unwinding.

app.MapGet("/tasks", (ITaskService svc) => svc.GetAll());

app.MapGet("/tasks/{id:int}", (int id, ITaskService svc) =>
    svc.GetById(id) is { } task ? Results.Ok(task) : Results.NotFound());

app.MapPost("/tasks", (TaskItem input, ITaskService svc) =>
{
    var created = svc.Add(input);
    return Results.Created($"/tasks/{created.Id}", created);
});

app.MapPut("/tasks/{id:int}", (int id, TaskItem input, ITaskService svc) =>
    svc.Update(id, input) is { } updated ? Results.Ok(updated) : Results.NotFound());

app.MapDelete("/tasks/{id:int}", (int id, ITaskService svc) =>
    svc.Delete(id) ? Results.NoContent() : Results.NotFound());

// Guarded by the gate middleware above: /admin/* needs the X-Api-Key header.
app.MapGet("/admin/stats", (ITaskService svc) =>
{
    var all = svc.GetAll().ToList();
    return Results.Ok(new { total = all.Count, done = all.Count(t => t.Done), open = all.Count(t => !t.Done) });
});

// The DI lifetime probe from episode 2.2.
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

// A deliberate failure raised from an ENDPOINT: a task that does not exist,
// dereferenced anyway. Exactly the kind of bug that reaches production.
app.MapGet("/diag/boom", (ITaskService svc) => Results.Ok(svc.GetById(999)!.Title));

app.Run();

static string Short(Guid id) => id.ToString()[..8];
