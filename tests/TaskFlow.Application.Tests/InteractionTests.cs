using Moq;
using TaskFlow.Application;
using TaskFlow.Domain;

namespace TaskFlow.Application.Tests;

// Moq earns its place when the INTERACTION is the requirement, or when a
// fake would need a special mode just to misbehave on cue.
public class InteractionTests
{
    // Episode 3.2's N+1 fix: the output is the same whether the service asks
    // for the counts once or once per task. Only a mock can see the difference.
    [Fact]
    public void Comment_summary_asks_for_all_the_counts_in_a_single_query()
    {
        var tasks = new Mock<ITaskRepository>();
        var comments = new Mock<ICommentRepository>();
        tasks.Setup(t => t.GetAll()).Returns(
        [
            new TaskItem { Id = 1, Title = "First" },
            new TaskItem { Id = 2, Title = "Second" },
            new TaskItem { Id = 3, Title = "Third" },
        ]);
        comments.Setup(c => c.CountByTask(It.IsAny<IReadOnlyCollection<int>>()))
                .Returns(new Dictionary<int, int> { [1] = 2, [3] = 1 });
        var service = new TaskService(tasks.Object, comments.Object);

        var summary = service.GetCommentSummary();

        Assert.Equal([2, 0, 1], summary.Select(s => s.CommentCount));
        comments.Verify(c => c.CountByTask(It.IsAny<IReadOnlyCollection<int>>()), Times.Once);
    }

    // Making a fake throw means adding a "fail now" switch to it. Moq does it in one line.
    [Fact]
    public void A_repository_failure_is_not_swallowed_and_nothing_is_saved()
    {
        var tasks = new Mock<ITaskRepository>();
        var comments = new Mock<ICommentRepository>();
        tasks.Setup(t => t.GetById(1)).Throws(new TimeoutException("database unavailable"));
        var service = new TaskService(tasks.Object, comments.Object);

        Assert.Throws<TimeoutException>(() => service.AddComment(1, "sam", "looks good"));

        comments.Verify(c => c.Add(It.IsAny<TaskComment>()), Times.Never);
    }
}
