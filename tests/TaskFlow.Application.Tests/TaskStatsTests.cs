using TaskFlow.Application;
using TaskFlow.Domain;

namespace TaskFlow.Application.Tests;

public class TaskStatsTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(3, 0)]
    [InlineData(1, 4)]
    public void Stats_count_the_done_and_the_open_tasks(int done, int open)
    {
        var tasks = Enumerable.Range(1, done + open)
            .Select(i => new TaskItem { Id = i, Title = $"Task {i}", Done = i <= done })
            .ToArray();
        var (service, _) = Arrange.Service(tasks);

        var stats = service.GetStats();

        Assert.Equal(new TaskStats(Total: done + open, Done: done, Open: open), stats);
    }
}
