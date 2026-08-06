using Microsoft.EntityFrameworkCore;

namespace TaskFlow;

// The DbContext: TaskFlow's session with the database.
//
// It is two things at once, and both matter:
//   1. a MODEL   — the class-to-table map EF Core builds once at startup and
//                  then reuses for every query it ever writes
//   2. a UNIT OF WORK — one short-lived object that tracks the entities you
//                  loaded, notices what you changed, and writes exactly those
//                  changes when you call SaveChanges()
//
// Because it tracks state per request, a DbContext is NOT thread-safe and is
// NOT meant to live long. AddDbContext registers it Scoped for that reason —
// one per HTTP request, disposed at the end of it.
public class TaskFlowDbContext : DbContext
{
    public TaskFlowDbContext(DbContextOptions<TaskFlowDbContext> options) : base(options) { }

    // A DbSet<T> is the queryable entry point for one entity type: the C# name
    // for a table. Writing _db.Tasks is what will eventually become
    // "SELECT ... FROM Tasks" — but nothing runs until you enumerate it.
    public DbSet<TaskItem> Tasks => Set<TaskItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var task = modelBuilder.Entity<TaskItem>();

        // EF Core would infer most of this by convention (Id -> primary key,
        // string -> TEXT, bool -> INTEGER). Configuring it explicitly is how a
        // team states the constraints it actually wants in the schema, instead
        // of whatever the defaults happen to be.
        task.ToTable("Tasks");
        task.HasKey(t => t.Id);
        task.Property(t => t.Id).ValueGeneratedOnAdd();

        task.Property(t => t.Title).IsRequired().HasMaxLength(80);
        task.Property(t => t.Priority).IsRequired().HasMaxLength(20).HasDefaultValue("normal");
        task.Property(t => t.CreatedBy).IsRequired().HasMaxLength(60);
        task.Property(t => t.InternalNotes).IsRequired().HasMaxLength(400).HasDefaultValue("");

        // An index is a schema decision, so it belongs in the model — and it
        // will show up in the migration as a real CREATE INDEX.
        task.HasIndex(t => t.Done).HasDatabaseName("IX_Tasks_Done");

        // Added after the first migration was already applied. EF will not
        // rewrite the first migration — it will generate a second one that
        // carries only the difference.
        task.HasIndex(t => t.Priority).HasDatabaseName("IX_Tasks_Priority");

        // Seed data. This is part of the MODEL, so EF puts it in the migration
        // as INSERT statements — a migration is not only DDL.
        task.HasData(
            new TaskItem
            {
                Id = 1, Title = "Set up the Git repo", Done = true, Priority = "normal",
                CreatedAt = new DateTimeOffset(2026, 7, 12, 9, 0, 0, TimeSpan.Zero),
                CreatedBy = "platform-team", InternalNotes = "billing code OPS-114",
            },
            new TaskItem
            {
                Id = 2, Title = "Open the first pull request", Done = true, Priority = "normal",
                CreatedAt = new DateTimeOffset(2026, 7, 12, 9, 5, 0, TimeSpan.Zero),
                CreatedBy = "platform-team", InternalNotes = "billing code OPS-114",
            },
            new TaskItem
            {
                Id = 3, Title = "Build the REST API", Done = false, Priority = "high",
                DueDate = new DateOnly(2026, 12, 1),
                CreatedAt = new DateTimeOffset(2026, 7, 12, 9, 30, 0, TimeSpan.Zero),
                CreatedBy = "platform-team", InternalNotes = "internal: blocked on the DTO refactor",
            });
    }
}
