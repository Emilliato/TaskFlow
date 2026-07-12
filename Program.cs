// TaskFlow — the course capstone, a small team task tracker.
using TaskFlow;

Console.WriteLine("TaskFlow v0.1");
Console.WriteLine("A team task tracker — scaffolded and version-controlled.");
Console.WriteLine();

TaskItem[] tasks =
[
    new(1, "Set up the Git repo", Done: true),
    new(2, "Open the first pull request", Done: false),
];

foreach (var task in tasks)
{
    var mark = task.Done ? "[x]" : "[ ]";
    Console.WriteLine($"{mark} #{task.Id} {task.Title}");
}
