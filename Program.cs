// TaskFlow API — episode 3.1. The endpoints, DTOs, validation and pipeline are
// exactly what 2.4 left behind. What changed is underneath: ITaskService is no
// longer a List<TaskItem> in memory, it is EF Core over a SQLite file. Nothing
// above the interface had to know.
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskFlow;

var builder = WebApplication.CreateBuilder(args);

// One log line per event, so the pipeline reads top-to-bottom in the console.
builder.Logging.AddSimpleConsole(options =>
{
    options.SingleLine = true;
    options.TimestampFormat = "HH:mm:ss.fff ";
});

// ---- Episode 3.1 ----------------------------------------------------------
// 0. The connection string lives in configuration, not in code. For SQLite it
//    is just a file path: one file, no server, no install.
var connectionString = builder.Configuration.GetConnectionString("TaskFlow")
                       ?? "Data Source=taskflow.db";

// 0b. AddDbContext registers the DbContext as SCOPED — one per HTTP request —
//     and hands it the provider to talk to. This single call is what turns
//     "some C# classes" into "an ORM with a database behind it".
builder.Services.AddDbContext<TaskFlowDbContext>(options =>
{
    options.UseSqlite(connectionString);

    // Development only. Without this, EF logs parameter values as '?' so a
    // production log file can never leak customer data. With it, you can see
    // exactly what was sent — which is why it must never ship enabled.
    if (builder.Environment.IsDevelopment())
        options.EnableSensitiveDataLogging();
});

// ---- Register services with the DI container (episode 2.2) ----
// SCOPED, not Singleton. It depends on the DbContext, and a longer-lived
// service may not capture a shorter-lived one. Episode 2.2's captive
// dependency rule stops being theory the moment a real DbContext shows up.
builder.Services.AddScoped<ITaskService, EfTaskService>();

builder.Services.AddTransient<ITransientOperation, Operation>(); // new every resolution
builder.Services.AddScoped<IScopedOperation, Operation>();       // one per request
builder.Services.AddSingleton<ISingletonOperation, Operation>(); // one forever
builder.Services.AddScoped<OperationLogger>();

// ---- Episode 2.4 ----------------------------------------------------------
// 1. One error shape for the whole API. Without this, a body that fails to
//    BIND returns a bare 400 with an empty body — technically correct, useless
//    to the caller.
builder.Services.AddProblemDetails();

// 2. Turn the data annotations on the DTOs into real request validation.
//    The attributes are only metadata; this is what makes something read them.
builder.Services.AddValidation();

// 3. The rules an attribute cannot express, as an injectable class.
builder.Services.AddScoped<IValidator<CreateTaskRequest>, CreateTaskRequestValidator>();

// 4. Publish the contract: a real OpenAPI (Swagger) document, generated from
//    the endpoints and the DTO types themselves.
builder.Services.AddOpenApi();

// 5. CORS. A browser will not let a page on one origin call an API on another
//    unless the API says so, in headers, on the response. This is the API's
//    half of that conversation — and it is an allow-LIST, not a switch.
builder.Services.AddCors(options => options.AddPolicy("dashboard", policy => policy
    .WithOrigins("https://taskflow-dashboard.example")
    .WithMethods("GET", "POST", "PUT", "DELETE")
    .WithHeaders("Content-Type", "X-Api-Key")));
// ---------------------------------------------------------------------------

var app = builder.Build();

// =====================  THE REQUEST PIPELINE (episode 2.3)  ================
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

// 1b. A 400 raised by MODEL BINDING has no body at all by default. This turns
//     every bodyless error response into the same RFC 7807 problem+json shape
//     the validators return, so a client only ever has to parse one thing.
app.UseStatusCodePages();

// 2. Correlation id: every request gets an id, on the way in and on the way out.
app.UseMiddleware<CorrelationIdMiddleware>();

// 3. Timing: measures everything registered BELOW it (and nothing above it).
app.UseMiddleware<RequestTimingMiddleware>();

// 4. Routing: matches the URL against the endpoint table. Above this line
//    nobody knows which endpoint will run; below it, everybody does.
app.UseRouting();

// 4b. CORS goes after routing and before the endpoints, so the preflight
//     OPTIONS request is answered by the policy and never reaches an endpoint.
app.UseCors("dashboard");

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

// 8. Serve the generated OpenAPI document, and a browsable UI over it.
app.MapOpenApi();                                    // -> /openapi/v1.json
app.UseSwaggerUI(options =>                          // -> /swagger
{
    options.SwaggerEndpoint("/openapi/v1.json", "TaskFlow v1");
    options.DocumentTitle = "TaskFlow API — Swagger UI";
});

// =====================  ENDPOINTS (terminal middleware)  ===================
// Every endpoint below speaks DTOs. The entity never crosses the wire.

app.MapGet("/tasks", (
        // MODEL BINDING: these come from the query string, by name, converted
        // to the declared type. Nothing here parses a string by hand.
        [FromQuery] bool? done,
        [FromQuery] string? search,
        ITaskService svc,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) =>
    {
        var query = svc.GetAll().Where(task => !task.IsArchived);

        if (done is not null)
            query = query.Where(task => task.Done == done);

        // Changed in 3.2. Title.Contains(search, StringComparison.OrdinalIgnoreCase)
        // reads fine and ran fine when GetAll() handed back an already-materialized
        // IEnumerable — LINQ-to-Objects can call any .NET method it likes. Now
        // that this composes onto a live IQueryable, the SQLite provider has to
        // translate the expression tree to SQL, and there is no SQL for "call
        // this specific .NET overload" — it throws InvalidOperationException at
        // query time instead of silently doing the wrong thing. EF.Functions.Like
        // is SQL from the start, so there is nothing to translate.
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(task => EF.Functions.Like(task.Title, $"%{search}%"));

        var results = query.Skip((page - 1) * pageSize).Take(pageSize)
                           .Select(task => task.ToResponse());   // entity -> DTO

        return Results.Ok(results);
    })
    .WithSummary("List tasks, filtered and paged.");

app.MapGet("/tasks/{id:int}", ([FromRoute] int id, ITaskService svc) =>
        svc.GetById(id) is { } task ? Results.Ok(task.ToResponse()) : Results.NotFound())
    .WithSummary("Get one task by id.")
    .Produces<TaskResponse>()
    .Produces(StatusCodes.Status404NotFound);

app.MapPost("/tasks", ([FromBody] CreateTaskRequest request, ITaskService svc) =>
    {
        // The request DTO has no Id, no CreatedAt, no CreatedBy, no
        // InternalNotes and no IsArchived — so an over-posted body has nothing
        // to bind to. The server fills those in, here, on its own terms.
        var created = svc.Add(request.ToEntity(createdBy: "api-client"));
        return Results.Created($"/tasks/{created.Id}", created.ToResponse());
    })
    .AddEndpointFilter<FluentValidationFilter<CreateTaskRequest>>()
    .WithSummary("Create a task.")
    .Produces<TaskResponse>(StatusCodes.Status201Created)
    .ProducesValidationProblem();

app.MapPut("/tasks/{id:int}", ([FromRoute] int id, [FromBody] UpdateTaskRequest request, ITaskService svc) =>
        svc.GetById(id) is { } existing
            ? Results.Ok(svc.Update(id, request.ApplyTo(existing))!.ToResponse())
            : Results.NotFound())
    .WithSummary("Replace a task.")
    .Produces<TaskResponse>()
    .ProducesValidationProblem();

app.MapDelete("/tasks/{id:int}", ([FromRoute] int id, ITaskService svc) =>
        svc.Delete(id) ? Results.NoContent() : Results.NotFound())
    .WithSummary("Delete a task.");

// A diagnostic endpoint whose only job is to report WHERE each argument came
// from. Model binding fills all five from completely different places.
app.MapPost("/binding/{id:int}", (
        [FromRoute] int id,
        [FromQuery] string? note,
        [FromHeader(Name = "X-Api-Key")] string? apiKey,
        [FromBody] CreateTaskRequest body,
        ITaskService svc) => Results.Ok(new
    {
        fromRoute = id,
        fromQuery = note,
        fromHeader = apiKey,
        fromBody = body.Title,
        fromServices = svc.GetType().Name,
    }))
    .WithSummary("Show where each bound argument came from.");

// Guarded by the gate middleware above: /admin/* needs the X-Api-Key header.
app.MapGet("/admin/stats", (ITaskService svc) =>
{
    var all = svc.GetAll().ToList();
    return Results.Ok(new { total = all.Count, done = all.Count(t => t.Done), open = all.Count(t => !t.Done) });
});

// ---- Episode 3.2: deferred execution, on purpose ----------------------------
// query is an IQueryable — a description of a SELECT, not a result. Building
// it sends nothing. It only becomes a real round trip to SQLite the moment
// something enumerates it — and it becomes ANOTHER one every time after that,
// because nothing here is caching what came back.
app.MapGet("/diag/deferred", (TaskFlowDbContext db) =>
{
    app.Logger.LogInformation("     LINQ      query variable built — nothing sent to the database yet");
    var query = db.Tasks.AsNoTracking().Where(t => !t.IsArchived);

    app.Logger.LogInformation("     LINQ      first enumeration: query.Count()");
    var count = query.Count();

    app.Logger.LogInformation("     LINQ      second enumeration: query.ToList() — same query variable");
    var list = query.ToList();

    return Results.Ok(new { count, returned = list.Count });
})
.WithSummary("Deferred execution: one query variable, enumerated twice, two round trips.");

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
