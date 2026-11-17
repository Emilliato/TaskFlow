using System.Diagnostics;
using System.IO.Compression;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;

namespace GrpcDemo;

public static class Client
{
    public static async Task RunAsync()
    {
        using var channel = GrpcChannel.ForAddress("http://localhost:5721");
        var grpc = new TaskCatalog.TaskCatalogClient(channel);
        using var http = new HttpClient { BaseAddress = new Uri("http://localhost:5722") };

        // 1. A call looks like calling a method: generated types, no URLs, no JSON.
        var one = await grpc.GetTaskAsync(new GetTaskRequest { Id = 7 });
        Console.WriteLine($"GetTask(7) -> id {one.Id}, '{one.Title}', done {one.Done}, priority {one.Priority}");
        Console.WriteLine();

        // 2. The same list, over both protocols: how many bytes is the message body?
        Console.WriteLine("tasks   JSON   JSON gzip   protobuf   protobuf gzip");
        foreach (var count in new[] { 1, 10, 100 })
        {
            var json = await http.GetByteArrayAsync($"/tasks?count={count}");
            var reply = await grpc.ListTasksAsync(new ListTasksRequest { Count = count });
            Console.WriteLine($"{count,5}   {json.Length,4}   {Gzip(json),8}   {reply.CalculateSize(),8}   {Gzip(reply.ToByteArray()),12}");
        }
        Console.WriteLine();

        // 3. Server streaming: replies arrive one by one, as the server produces them.
        var clock = Stopwatch.StartNew();
        using var call = grpc.WatchTasks(new ListTasksRequest { Count = 4 });
        await foreach (var task in call.ResponseStream.ReadAllAsync())
            Console.WriteLine($"stream: task {task.Id} arrived at {clock.ElapsedMilliseconds} ms");
    }

    private static int Gzip(byte[] bytes)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true)) gzip.Write(bytes);
        return (int)output.Length;
    }
}
