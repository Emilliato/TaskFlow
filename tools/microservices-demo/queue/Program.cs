// A slow job (about 2 seconds: think "send a welcome email") handled two ways.
//   inline   the caller waits for every job to finish
//   publish  the caller puts a message on a queue and moves on
//   consume  a separate process takes messages off the queue and does the slow work
using System.Diagnostics;
using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

const string QueueName = "welcome-emails";
const int JobCount = 5;

var factory = new ConnectionFactory
{
    HostName = "localhost",
    Port = int.Parse(Environment.GetEnvironmentVariable("RABBIT_PORT") ?? "5672"),
    UserName = Environment.GetEnvironmentVariable("RABBIT_USER") ?? "guest",
    Password = Environment.GetEnvironmentVariable("RABBIT_PASSWORD") ?? "guest",
};

switch (args.FirstOrDefault())
{
    case "inline": await Inline(); break;
    case "publish": await Publish(); break;
    case "consume": await Consume(); break;
    default: Console.WriteLine("usage: inline | publish | consume"); break;
}

static async Task SlowJob()
{
    await Task.Delay(2000);                       // stands in for a slow dependency
}

static string Seconds(Stopwatch sw) => (sw.Elapsed.TotalMilliseconds / 1000).ToString("0.00") + " s";

static async Task Inline()
{
    var total = Stopwatch.StartNew();
    for (var n = 1; n <= JobCount; n++)
    {
        await SlowJob();
        Console.WriteLine($"request {n}: answered after {Seconds(total)}");
    }
}

async Task Publish()
{
    await using var connection = await factory.CreateConnectionAsync();
    await using var channel = await connection.CreateChannelAsync();
    // Durable: the queue survives a broker restart. Declaring is idempotent: safe to run again.
    await channel.QueueDeclareAsync(QueueName, durable: true, exclusive: false, autoDelete: false);

    var total = Stopwatch.StartNew();
    for (var n = 1; n <= JobCount; n++)
    {
        var body = Encoding.UTF8.GetBytes($"welcome email {n}");
        // Persistent: the broker writes the message to disk, not only to memory.
        await channel.BasicPublishAsync("", QueueName, mandatory: false,
            basicProperties: new BasicProperties { Persistent = true }, body: body);
        Console.WriteLine($"request {n}: answered after {Seconds(total)}  (queued: welcome email {n})");
    }
}

async Task Consume()
{
    await using var connection = await factory.CreateConnectionAsync();
    await using var channel = await connection.CreateChannelAsync();
    await channel.QueueDeclareAsync(QueueName, durable: true, exclusive: false, autoDelete: false);
    await channel.BasicQosAsync(0, 1, false);       // one unacknowledged message at a time

    var total = Stopwatch.StartNew();
    var done = 0;
    var finished = new TaskCompletionSource();
    var consumer = new AsyncEventingBasicConsumer(channel);
    consumer.ReceivedAsync += async (_, delivery) =>
    {
        var text = Encoding.UTF8.GetString(delivery.Body.Span);
        await SlowJob();
        await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false);   // done: the broker may forget it
        Console.WriteLine($"consumer: finished '{text}' at {Seconds(total)}");
        if (++done == JobCount) finished.TrySetResult();
    };
    await channel.BasicConsumeAsync(QueueName, autoAck: false, consumer);
    await finished.Task;
}
