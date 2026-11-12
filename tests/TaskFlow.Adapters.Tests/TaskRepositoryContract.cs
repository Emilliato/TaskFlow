using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application;
using TaskFlow.Application.Tests;
using TaskFlow.Domain;
using TaskFlow.Infrastructure;

namespace TaskFlow.Adapters.Tests;

// ======================= ONE CONTRACT, EVERY IMPLEMENTATION =================
// ITaskRepository is a promise. Anything that implements it must keep the
// promise, or the code that was written against the interface breaks when it
// is handed a different implementation. That is the Liskov Substitution
// Principle, and this is how you test it: write the promise once, then run
// the SAME tests against every implementation.
// ===========================================================================
public abstract class TaskRepositoryContract
{
    protected abstract ITaskRepository Create();

    [Fact]
    public void Add_assigns_an_id()
    {
        var added = Create().Add(new TaskItem { Title = "contract: assigns an id" });

        Assert.NotEqual(0, added.Id);
    }

    [Fact]
    public void A_task_that_was_added_can_be_found_by_its_id()
    {
        var repo = Create();
        var added = repo.Add(new TaskItem { Title = "contract: find me" });

        Assert.Equal("contract: find me", repo.GetById(added.Id)?.Title);
    }

    [Fact]
    public void Search_by_text_returns_only_the_tasks_that_contain_it()
    {
        var repo = Create();
        repo.Add(new TaskItem { Title = "contract: alpha release" });
        repo.Add(new TaskItem { Title = "contract: beta release" });

        var found = repo.Search(new TaskFilter(null, "alpha"));

        Assert.Equal(["contract: alpha release"], found.Select(t => t.Title));
    }

    [Fact]
    public void Search_treats_a_percent_sign_as_text_not_as_a_wildcard()
    {
        var repo = Create();
        repo.Add(new TaskItem { Title = "contract: plain title" });
        repo.Add(new TaskItem { Title = "contract: 100% done" });

        var found = repo.Search(new TaskFilter(null, "%"));

        Assert.Equal(["contract: 100% done"], found.Select(t => t.Title));
    }

    [Fact]
    public void Update_returns_the_task_as_it_is_now()
    {
        var repo = Create();
        var added = repo.Add(new TaskItem { Title = "contract: before" });

        var updated = repo.Update(added.Id, added with { Title = "contract: after", Done = true });

        Assert.Equal("contract: after", updated?.Title);
        Assert.True(updated?.Done);
        Assert.Equal("contract: after", repo.GetById(added.Id)?.Title);
    }

    [Fact]
    public void Update_of_a_task_that_does_not_exist_returns_null()
    {
        Assert.Null(Create().Update(9999, new TaskItem { Title = "contract: nobody" }));
    }

    [Fact]
    public void Title_lookup_ignores_case_and_surrounding_spaces()
    {
        var repo = Create();
        repo.Add(new TaskItem { Title = "Contract: Mixed Case" });

        Assert.True(repo.TitleExists("  contract: mixed case "));
    }
}

public class FakeTaskRepositoryContractTests : TaskRepositoryContract
{
    protected override ITaskRepository Create() => new FakeTaskRepository([]);
}

public class InMemoryTaskRepositoryContractTests : TaskRepositoryContract
{
    protected override ITaskRepository Create() => new InMemoryTaskRepository();
}

public class EfTaskRepositoryContractTests : TaskRepositoryContract, IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    protected override ITaskRepository Create()
    {
        _connection.Open();
        var db = new TaskFlowDbContext(new DbContextOptionsBuilder<TaskFlowDbContext>().UseSqlite(_connection).Options);
        db.Database.EnsureCreated();
        return new EfTaskRepository(db);
    }

    public void Dispose() => _connection.Dispose();
}
