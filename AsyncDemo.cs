namespace TaskFlow;

// Episode 3.4. The same 300 ms webhook call, written two ways.
public static class AsyncDemo
{
    public static void MapAsyncDemo(this WebApplication app)
    {
        var log = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("TaskFlow.Demo");

        // Does nothing at all. If THIS is slow, the whole server is starved.
        app.MapGet("/ping", () => Results.Ok(new { pong = true }));

        // What the thread pool looks like right now.
        app.MapGet("/diag/pool", () => Results.Ok(new
        {
            threads = ThreadPool.ThreadCount,
            queued = ThreadPool.PendingWorkItemCount,
        }));

        // THE MISTAKE: .Result blocks the calling thread until the Task finishes.
        app.MapGet("/demo/blocking/{id}", (string id, IWebhookGateway gateway) =>
        {
            var receipt = gateway.SendAsync($"task-{id}").Result;
            return Results.Ok(new { receipt });
        });

        // THE FIX: await hands the thread back to the pool while the call is in flight.
        app.MapGet("/demo/async/{id}", async (string id, IWebhookGateway gateway, CancellationToken ct) =>
        {
            var receipt = await gateway.SendAsync($"task-{id}", ct);
            return Results.Ok(new { receipt });
        });

        // The same fix, with the evidence: which thread runs each half.
        app.MapGet("/demo/where-am-i", async (IWebhookGateway gateway) =>
        {
            log.LogInformation("THREAD   before await: thread {Id}  SynchronizationContext.Current = {Ctx}",
                Environment.CurrentManagedThreadId, SynchronizationContext.Current?.ToString() ?? "null");

            var receipt = await gateway.SendAsync("task-where");

            log.LogInformation("THREAD   after  await: thread {Id}",
                Environment.CurrentManagedThreadId);
            return Results.Ok(new { receipt });
        });

        // ---- Two more ways to get async wrong ------------------------------

        // MISTAKE 2: async void. Nobody can await it, so an exception inside it
        // has no caller to travel to. It is raised on the thread pool instead,
        // and an unhandled exception there ends the process.
        app.MapGet("/demo/async-void/{id}", (string id, IWebhookGateway gateway) =>
        {
            NotifyAndForget(gateway, $"task-{id}");
            return Results.Accepted();
        });

        // THE FIX: return Task, and await it. The failure travels back to the
        // request, where the pipeline turns it into a 500 and the server lives on.
        app.MapGet("/demo/async-task/{id}", async (string id, IWebhookGateway gateway) =>
        {
            await NotifyAsync(gateway, $"task-{id}");
            return Results.Accepted();
        });

        // MISTAKE 3: the call is made but never awaited. The response goes out
        // while the work is still running, and a failure is never seen by anyone.
        // (The compiler warns about exactly this: CS4014. Silenced here only
        // because this endpoint exists to be wrong.)
#pragma warning disable CS4014
        app.MapGet("/demo/forgot-await/{id}", async (string id, IWebhookGateway gateway) =>
        {
            gateway.SendAsync($"task-{id}");
            return Results.Ok(new { sent = true });
        });
#pragma warning restore CS4014

        // MISTAKE 4: Task.Run around blocking work only MOVES the blocked thread.
        // The call looks async and returns a Task, but a pool thread still sits
        // in Thread.Sleep for the whole 300 ms.
        app.MapGet("/demo/fake-async/{id}", async (string id) =>
        {
            var receipt = await Task.Run(() =>
            {
                Thread.Sleep(300);
                return $"delivered:task-{id}";
            });
            return Results.Ok(new { receipt });
        });

        // ---- A Task is a value you can hold ---------------------------------

        // Three independent calls, awaited one after another: 3 x 300 ms.
        app.MapGet("/demo/fan-out/sequential/{id}", async (string id, IWebhookGateway gateway, CancellationToken ct) =>
        {
            var a = await gateway.SendAsync($"a-{id}", ct);
            var b = await gateway.SendAsync($"b-{id}", ct);
            var c = await gateway.SendAsync($"c-{id}", ct);
            return Results.Ok(new { a, b, c });
        });

        // The same three calls, started together and awaited together: about 300 ms.
        app.MapGet("/demo/fan-out/parallel/{id}", async (string id, IWebhookGateway gateway, CancellationToken ct) =>
        {
            var a = gateway.SendAsync($"a-{id}", ct);   // started, not awaited yet
            var b = gateway.SendAsync($"b-{id}", ct);
            var c = gateway.SendAsync($"c-{id}", ct);
            await Task.WhenAll(a, b, c);
            return Results.Ok(new { a = await a, b = await b, c = await c });
        });
    }

    static async void NotifyAndForget(IWebhookGateway gateway, string payload) =>
        await gateway.SendAsync(payload);

    static async Task NotifyAsync(IWebhookGateway gateway, string payload) =>
        await gateway.SendAsync(payload);

    // Samples the thread pool from its OWN thread (not a pool thread), so the
    // numbers keep flowing even while the pool is starved.
    public static void StartPoolMonitor(ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("TaskFlow.Pool");
        var monitor = new Thread(() =>
        {
            var lastThreads = -1;
            while (true)
            {
                Thread.Sleep(500);
                var threads = ThreadPool.ThreadCount;
                var pending = ThreadPool.PendingWorkItemCount;
                if (pending > 0 || threads != lastThreads)
                    logger.LogInformation("POOL   threads={Threads,3}  queued={Pending}", threads, pending);
                lastThreads = threads;
            }
        }) { IsBackground = true, Name = "pool-monitor" };
        monitor.Start();
    }
}
