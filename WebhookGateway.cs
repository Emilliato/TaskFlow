namespace TaskFlow;

// An outbound call TaskFlow makes when something happens: tell another system
// "this task changed". In production this is an HttpClient POST to somebody
// else's server. The shape that matters is the signature: the work finishes
// LATER, so the method hands back a Task<T> instead of a value.
public interface IWebhookGateway
{
    Task<string> SendAsync(string payload, CancellationToken cancellationToken = default);
}

public class SlowWebhookGateway(ILogger<SlowWebhookGateway> logger) : IWebhookGateway
{
    public async Task<string> SendAsync(string payload, CancellationToken cancellationToken = default)
    {
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        try
        {
            // Stands in for a 300 ms network round trip. Task.Delay is a timer,
            // not a thread sitting in a loop: while it runs, no thread is used.
            await Task.Delay(300, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // The caller gave up (a client disconnect trips the token). Stop paying for work nobody wants.
            logger.LogInformation("CANCEL  webhook '{Payload}' abandoned after {Ms:F0} ms",
                payload, System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            throw;
        }

        if (payload.Contains("bad"))
            throw new InvalidOperationException($"Webhook rejected '{payload}' (HTTP 502).");

        return $"delivered:{payload}";
    }
}
