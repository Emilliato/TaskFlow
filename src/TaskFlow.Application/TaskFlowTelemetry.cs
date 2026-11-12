using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace TaskFlow.Application;

// The application layer's own telemetry, built from types that ship in the
// runtime (System.Diagnostics). No OpenTelemetry package is needed here: this
// layer only RAISES spans and counts. Whether anything listens, and where it
// sends them, is decided by the host.
public static class TaskFlowTelemetry
{
    public const string Name = "TaskFlow.Application";

    // A span: "this piece of work started, and here is how long it took".
    public static readonly ActivitySource Source = new(Name);

    // A metric: a number that is only ever added to, with dimensions to slice by.
    private static readonly Meter Meter = new(Name);
    public static readonly Counter<int> CommentsAdded = Meter.CreateCounter<int>("taskflow.comments.added");
}
