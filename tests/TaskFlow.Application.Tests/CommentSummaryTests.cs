using TaskFlow.Application;
using TaskFlow.Domain;

namespace TaskFlow.Application.Tests;

public class CommentSummaryTests
{
    [Fact]
    public void Summary_lists_every_task_with_its_number_of_comments()
    {
        var (service, _) = Arrange.Service(
            new TaskItem { Id = 1, Title = "First" },
            new TaskItem { Id = 2, Title = "Second" },
            new TaskItem { Id = 3, Title = "Third" });
        service.AddComment(1, "sam", "one");
        service.AddComment(1, "lee", "two");
        service.AddComment(3, "sam", "three");

        var summary = service.GetCommentSummary();

        Assert.Equal([2, 0, 1], summary.Select(s => s.CommentCount));
    }
}
