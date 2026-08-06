using System.Diagnostics;

namespace TaskFlow;

// ---------------------------------------------------------------------------
// A middleware is just a class with:
//   * a constructor whose first parameter is the NEXT middleware, and
//   * an InvokeAsync(HttpContext).
// Everything before `await next(context)` runs on the way IN; everything after
// it runs on the way OUT. That two-pass shape is the whole mental model: the
// pipeline is a set of nested wrappers around your endpoint, not a list of
// steps that run one after another.
// ---------------------------------------------------------------------------

/// <summary>Stamps every request/response with an id you can grep for in logs.</summary>
public class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        // Reuse the caller's id if it sent one, otherwise mint a short one.
        var correlationId =
            context.Request.Headers.TryGetValue(HeaderName, out var incoming) &&
            !string.IsNullOrWhiteSpace(incoming)
                ? incoming.ToString()
                : Guid.NewGuid().ToString("N")[..8];

        // Anything further down the pipeline can read this off HttpContext.
        context.Items[HeaderName] = correlationId;

        // Response headers are read-only once the response has started, so we
        // register a callback that runs at the moment Kestrel starts sending.
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        logger.LogInformation("--> IN   correlation  {Id}", correlationId);
        await next(context);                       // hand off to the rest of the pipeline
        logger.LogInformation("<-- OUT  correlation  {Id}", correlationId);
    }
}

/// <summary>Times everything registered BELOW it — nothing above.</summary>
public class RequestTimingMiddleware(RequestDelegate next, ILogger<RequestTimingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        logger.LogInformation("--> IN   timing       {Method} {Path}",
            context.Request.Method, context.Request.Path);

        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-Response-Time-ms"] =
                stopwatch.Elapsed.TotalMilliseconds.ToString("0.00");
            return Task.CompletedTask;
        });

        await next(context);

        stopwatch.Stop();
        logger.LogInformation("<-- OUT  timing       {Status} in {Elapsed:0.00} ms",
            context.Response.StatusCode, stopwatch.Elapsed.TotalMilliseconds);
    }
}

/// <summary>
/// A gate. When it refuses a request it writes the response and simply does NOT
/// call next() — that is "short-circuiting": nothing below it, including the
/// endpoint routing already picked, ever runs.
/// </summary>
public class ApiKeyGateMiddleware(RequestDelegate next, ILogger<ApiKeyGateMiddleware> logger)
{
    private const string HeaderName = "X-Api-Key";
    private const string ExpectedKey = "taskflow-demo-key";  // real secret handling: episode 4.3

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/admin"))
        {
            if (!context.Request.Headers.TryGetValue(HeaderName, out var key) || key != ExpectedKey)
            {
                // Routing already selected an endpoint, and it will never run.
                logger.LogWarning("     GATE      401 short-circuit: {Endpoint} never runs",
                    context.GetEndpoint()?.DisplayName ?? "(none)");

                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { error = "Missing or invalid X-Api-Key" });
                return;                            // <-- no next(): the pipeline stops here
            }

            logger.LogInformation("     GATE      key accepted for {Path}", context.Request.Path);
        }

        await next(context);
    }
}
