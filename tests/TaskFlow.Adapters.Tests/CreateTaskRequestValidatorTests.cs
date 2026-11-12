using TaskFlow.Api;
using TaskFlow.Application.Tests;
using TaskFlow.Domain;

namespace TaskFlow.Adapters.Tests;

public class CreateTaskRequestValidatorTests
{
    // The validator asks one question: does a task with this title exist?
    // But it takes a TaskService, so the test has to build a whole one.
    [Fact]
    public async Task A_title_that_already_exists_is_rejected()
    {
        var (service, _) = Arrange.Service(new TaskItem { Id = 1, Title = "Ship it" });
        var validator = new CreateTaskRequestValidator(service);

        var result = await validator.ValidateAsync(new CreateTaskRequest { Title = "Ship it" });

        Assert.Contains(result.Errors, e => e.ErrorCode == "TITLE_ALREADY_EXISTS");
    }

    [Fact]
    public async Task A_new_title_is_accepted()
    {
        var (service, _) = Arrange.Service(new TaskItem { Id = 1, Title = "Ship it" });
        var validator = new CreateTaskRequestValidator(service);

        var result = await validator.ValidateAsync(new CreateTaskRequest { Title = "Write the docs" });

        Assert.True(result.IsValid);
    }
}
