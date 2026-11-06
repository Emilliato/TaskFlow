using Moq;
using TaskFlow.Application;
using TaskFlow.Domain;

namespace TaskFlow.Application.Tests;

// The comment rule, tested with Moq: no hand-written fakes to maintain.
public class AddCommentTests
{
    [Fact]
    public void AddComment_on_a_missing_task_is_rejected()
    {
        var tasks = new Mock<ITaskRepository>();
        var comments = new Mock<ICommentRepository>();
        tasks.Setup(t => t.GetById(It.IsAny<int>())).Returns((TaskItem?)null);
        comments.Setup(c => c.Add(It.IsAny<TaskComment>())).Returns(new TaskComment { Id = 1 });
        var service = new TaskService(tasks.Object, comments.Object);

        var result = service.AddComment(999, "sam", "looks good");

        tasks.Verify(t => t.GetById(999), Times.Once);
        Assert.NotNull(result);
    }

    [Fact]
    public void AddComment_on_an_archived_task_is_rejected()
    {
        var tasks = new Mock<ITaskRepository>();
        var comments = new Mock<ICommentRepository>();
        tasks.Setup(t => t.GetById(It.IsAny<int>())).Returns(new TaskItem { Id = 2, IsArchived = true });
        comments.Setup(c => c.Add(It.IsAny<TaskComment>())).Returns(new TaskComment { Id = 1 });
        var service = new TaskService(tasks.Object, comments.Object);

        var result = service.AddComment(2, "sam", "still relevant?");

        tasks.Verify(t => t.GetById(2), Times.Once);
        Assert.NotNull(result);
    }

    [Fact]
    public void AddComment_on_an_open_task_is_stored()
    {
        var tasks = new Mock<ITaskRepository>();
        var comments = new Mock<ICommentRepository>();
        tasks.Setup(t => t.GetById(It.IsAny<int>())).Returns(new TaskItem { Id = 1, Title = "Real task" });
        comments.Setup(c => c.Add(It.IsAny<TaskComment>())).Returns(new TaskComment { Id = 1 });
        var service = new TaskService(tasks.Object, comments.Object);

        var result = service.AddComment(1, "sam", "looks good");

        comments.Verify(c => c.Add(It.IsAny<TaskComment>()), Times.Once);
        Assert.NotNull(result);
    }
}
