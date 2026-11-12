using TaskFlow.Api;
using TaskFlow.Application;

namespace TaskFlow.Adapters.Tests;

public class CreateTaskRequestValidatorTests
{
    // The validator asks one question, so the test answers one question.
    private sealed class StubTitles(params string[] existing) : ITaskTitles
    {
        public bool TitleExists(string title) => existing.Contains(title.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_title_that_already_exists_is_rejected()
    {
        var validator = new CreateTaskRequestValidator(new StubTitles("Ship it"));

        var result = await validator.ValidateAsync(new CreateTaskRequest { Title = "Ship it" });

        Assert.Contains(result.Errors, e => e.ErrorCode == "TITLE_ALREADY_EXISTS");
    }

    [Fact]
    public async Task A_new_title_is_accepted()
    {
        var validator = new CreateTaskRequestValidator(new StubTitles("Ship it"));

        var result = await validator.ValidateAsync(new CreateTaskRequest { Title = "Write the docs" });

        Assert.True(result.IsValid);
    }
}
