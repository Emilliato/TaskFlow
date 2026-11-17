using GrpcDemo;

// dotnet run -- server   starts the service (gRPC on 5721, plain JSON REST on 5722)
// dotnet run -- client   calls it both ways and compares what went over the wire
if (args.FirstOrDefault() == "client")
{
    await Client.RunAsync();
    return;
}

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddGrpc();
builder.WebHost.ConfigureKestrel(kestrel =>
{
    // gRPC needs HTTP/2. Without TLS that means "prior knowledge" HTTP/2, so this port speaks HTTP/2 only.
    kestrel.ListenLocalhost(5721, o => o.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2);
    kestrel.ListenLocalhost(5722, o => o.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1);
});

var app = builder.Build();
app.MapGrpcService<TaskCatalogService>();

// The same data as plain REST and JSON, so the two can be compared fairly.
app.MapGet("/tasks", (int count) => Results.Ok(Catalog.Make(count)));

app.Run();
