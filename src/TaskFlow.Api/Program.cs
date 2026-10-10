// TaskFlow API — episode 3.3. This project is now the OUTERMOST, thinnest layer:
// it turns HTTP into calls on the application layer (TaskService) and turns the
// answers back into status codes and DTOs. It never touches a DbContext. The
// only place it meets EF Core at all is the single AddInfrastructure call below.
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api;
using TaskFlow.Application;
using TaskFlow.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// One log line per event, so the pipeline reads top-to-bottom in the console.
builder.Logging.AddSimpleConsole(options =>
{
    options.SingleLine = true;
    options.TimestampFormat = "HH:mm:ss.fff ";
});

// ---- Episode 3.3: compose the layers --------------------------------------
// The connection string lives in configuration, not in code.
var connectionString = builder.Configuration.GetConnectionString("TaskFlow")
                       ?? "Data Source=taskflow.db";

// ONE call plugs in the whole outer layer: the DbContext, SQLite, and the
// implementations of ITaskRepository / ICommentRepository. The sensitive-data
// flag is Development only, for the reason it always was.
builder.Services.AddInfrastructure(connectionString, builder.Environment.IsDevelopment());

// The application layer: Scoped, because it holds repositories that hold a
// scoped DbContext (episode 2.2's captive-dependency rule, still in force).
builder.Services.AddScoped<TaskService>();

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
        [FromQuery] bool? done,
        [FromQuery] string? search,
        TaskService svc,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) =>
        Results.Ok(svc.List(new TaskFilter(done, search, page, pageSize))
                      .Select(task => task.ToResponse())))
    .WithSummary("List tasks, filtered and paged.");

app.MapGet("/tasks/{id:int}", ([FromRoute] int id, TaskService svc) =>
        svc.Get(id) is { } task ? Results.Ok(task.ToResponse()) : Results.NotFound())
    .WithSummary("Get one task by id.")
    .Produces<TaskResponse>()
    .Produces(StatusCodes.Status404NotFound);

app.MapPost("/tasks", ([FromBody] CreateTaskRequest request, TaskService svc) =>
    {
        // The request DTO has no Id, no CreatedAt, no CreatedBy, no
        // InternalNotes and no IsArchived — so an over-posted body has nothing
        // to bind to. The server fills those in, here, on its own terms.
        var created = svc.Create(request.ToEntity(createdBy: "api-client"));
        return Results.Created($"/tasks/{created.Id}", created.ToResponse());
    })
    .AddEndpointFilter<FluentValidationFilter<CreateTaskRequest>>()
    .WithSummary("Create a task.")
    .Produces<TaskResponse>(StatusCodes.Status201Created)
    .ProducesValidationProblem();

app.MapPut("/tasks/{id:int}", ([FromRoute] int id, [FromBody] UpdateTaskRequest request, TaskService svc) =>
        svc.Get(id) is { } existing
            ? Results.Ok(svc.Update(id, request.ApplyTo(existing))!.ToResponse())
            : Results.NotFound())
    .WithSummary("Replace a task.")
    .Produces<TaskResponse>()
    .ProducesValidationProblem();

app.MapDelete("/tasks/{id:int}", ([FromRoute] int id, TaskService svc) =>
        svc.Delete(id) ? Results.NoContent() : Results.NotFound())
    .WithSummary("Delete a task.");

// Guarded by the gate middleware above: /admin/* needs the X-Api-Key header.
app.MapGet("/admin/stats", (TaskService svc) => Results.Ok(svc.GetStats()));

// ---- Comments: the endpoint that used to write straight to the DbContext ----
// Look at what is left. Ask the application layer; translate its answer into
// HTTP. The rule itself (does the task exist? is it archived?) lives in
// TaskService.AddComment, where a unit test can reach it without a web server.
app.MapPost("/tasks/{id:int}/comments", ([FromRoute] int id, [FromBody] AddCommentRequest request, TaskService svc) =>
    {
        var result = svc.AddComment(id, request.Author, request.Body);
        return result.Outcome switch
        {
            CommentOutcome.Added => Results.Created(
                $"/tasks/{id}/comments/{result.Comment!.Id}", result.Comment.ToResponse()),
            CommentOutcome.TaskNotFound => Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Task not found.",
                detail: $"There is no task {id} to comment on."),
            _ => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Task is archived.",
                detail: $"Task {id} is archived and no longer accepts comments."),
        };
    })
    .WithSummary("Add a comment to a task.");

// The N+1 fix from 3.2, now one line of HTTP over one use case.
app.MapGet("/tasks/summary", (TaskService svc) => Results.Ok(svc.GetCommentSummary()))
    .WithSummary("Every task with its comment count, in two queries total.");

// A diagnostic endpoint whose only job is to report WHERE each argument came
// from. Model binding fills all five from completely different places.
app.MapPost("/binding/{id:int}", (
        [FromRoute] int id,
        [FromQuery] string? note,
        [FromHeader(Name = "X-Api-Key")] string? apiKey,
        [FromBody] CreateTaskRequest body,
        TaskService svc) => Results.Ok(new
    {
        fromRoute = id,
        fromQuery = note,
        fromHeader = apiKey,
        fromBody = body.Title,
        fromServices = svc.GetType().Name,
    }))
    .WithSummary("Show where each bound argument came from.");

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
app.MapGet("/diag/boom", (TaskService svc) => Results.Ok(svc.Get(999)!.Title));

app.Run();

static string Short(Guid id) => id.ToString()[..8];
