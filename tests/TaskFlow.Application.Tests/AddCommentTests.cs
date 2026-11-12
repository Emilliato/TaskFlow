using TaskFlow.Application;
using TaskFlow.Domain;

namespace TaskFlow.Application.Tests;

public class AddCommentTests
{
    // A [Theory] runs the same test body once per [InlineData] row.
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(999)]
    public void Comment_on_a_task_that_does_not_exist_is_rejected_and_nothing_is_stored(int taskId)
    {
        // Arrange
        var (service, comments) = Arrange.Service(new TaskItem { Id = 1, Title = "Real task" });

        // Act
        var result = service.AddComment(taskId, "sam", "looks good");

        // Assert
        Assert.Equal(CommentOutcome.TaskNotFound, result.Outcome);
        Assert.Empty(comments.Stored);
    }

    [Fact]
    public void Comment_on_an_archived_task_is_rejected_and_nothing_is_stored()
    {
        var (service, comments) = Arrange.Service(new TaskItem { Id = 2, Title = "Old task", IsArchived = true });

        var result = service.AddComment(2, "sam", "still relevant?");

        Assert.Equal(CommentOutcome.TaskArchived, result.Outcome);
        Assert.Empty(comments.Stored);
    }

    [Fact]
    public void Comment_on_an_open_task_is_stored()
    {
        var (service, comments) = Arrange.Service(new TaskItem { Id = 1, Title = "Real task" });

        var result = service.AddComment(1, "sam", "looks good");

        Assert.Equal(CommentOutcome.Added, result.Outcome);
        var stored = Assert.Single(comments.Stored);
        Assert.Equal(1, stored.TaskId);
        Assert.Equal("looks good", stored.Body);
    }

    // The 500 from production: a task created through the API has CreatedBy "api-client",
    // an owner the service had never heard of.
    [Fact]
    public void Comment_on_a_task_created_through_the_API_is_stored()
    {
        var (service, comments) = Arrange.Service(new TaskItem { Id = 4, Title = "Created by the API", CreatedBy = "api-client" });

        var result = service.AddComment(4, "sam", "looks good");

        Assert.Equal(CommentOutcome.Added, result.Outcome);
        Assert.Single(comments.Stored);
    }
}
