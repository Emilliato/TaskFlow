using Grpc.Core;

namespace GrpcDemo;

// TaskCatalogBase is generated from tasks.proto. The service only fills in the methods.
public class TaskCatalogService : TaskCatalog.TaskCatalogBase
{
    public override Task<TaskReply> GetTask(GetTaskRequest request, ServerCallContext context) =>
        Task.FromResult(Catalog.Make(request.Id)[^1].ToReply());

    public override Task<ListTasksReply> ListTasks(ListTasksRequest request, ServerCallContext context)
    {
        var reply = new ListTasksReply();
        reply.Tasks.AddRange(Catalog.Make(request.Count).Select(row => row.ToReply()));
        return Task.FromResult(reply);
    }

    // Server streaming: one request, then a reply every 300 ms, on the same connection.
    public override async Task WatchTasks(ListTasksRequest request, IServerStreamWriter<TaskReply> stream, ServerCallContext context)
    {
        foreach (var row in Catalog.Make(request.Count))
        {
            await stream.WriteAsync(row.ToReply());
            await Task.Delay(300, context.CancellationToken);
        }
    }
}
