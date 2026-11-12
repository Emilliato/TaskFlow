using System.Diagnostics;
using Microsoft.Extensions.Logging;

// What does a log call cost when the level is switched OFF?
// A real logger, a real console provider, minimum level Information. Every call
// below is a Debug call, so nothing is ever written: any cost is pure overhead.
using var factory = LoggerFactory.Create(b => b.SetMinimumLevel(LogLevel.Information).AddConsole());
var logger = factory.CreateLogger("Hot");

const int Calls = 10_000_000;
var user = "ana";

#pragma warning disable CA2254   // deliberate: this is the mistake being measured
Measure("interpolated string", () =>
{
    for (var i = 0; i < Calls; i++) logger.LogDebug($"Loaded task {i} for {user}");
});

#pragma warning restore CA2254

Measure("message template", () =>
{
    for (var i = 0; i < Calls; i++) logger.LogDebug("Loaded task {TaskId} for {User}", i, user);
});

Measure("template behind IsEnabled", () =>
{
    for (var i = 0; i < Calls; i++)
        if (logger.IsEnabled(LogLevel.Debug)) logger.LogDebug("Loaded task {TaskId} for {User}", i, user);
});

Measure("[LoggerMessage]", () =>
{
    for (var i = 0; i < Calls; i++) Log.LoadedTask(logger, i, user);
});

static void Measure(string name, Action work)
{
    work();                                              // warm-up: JIT and first-call costs
    GC.Collect(); GC.WaitForPendingFinalizers();
    var bytes0 = GC.GetAllocatedBytesForCurrentThread();
    var sw = Stopwatch.StartNew();
    work();
    sw.Stop();
    var bytes = GC.GetAllocatedBytesForCurrentThread() - bytes0;
    Console.WriteLine($"{name,-28} {sw.Elapsed.TotalMilliseconds * 1_000_000 / Calls,7:0.0} ns/call {(double)bytes / Calls,7:0.0} bytes/call");
}

// The source generator writes the body of this method at compile time: it checks
// the level first, and it takes typed arguments, so nothing is boxed or built.
static partial class Log
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "Loaded task {TaskId} for {User}")]
    public static partial void LoadedTask(ILogger logger, int taskId, string user);
}
