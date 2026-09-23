using System.ComponentModel.DataAnnotations;

namespace TaskFlow;

// ============================ THE DTOs =====================================
// A DTO — Data Transfer Object — is a type whose only job is to describe what
// crosses the wire. It is shaped by the CONTRACT, not by the database.
//
// Two of them, because the two directions are not the same shape:
//   CreateTaskRequest   what a client is allowed to SEND
//   TaskResponse        what a client is allowed to SEE
//
// Everything the server owns is simply absent from the request DTO, so there
// is nothing for an over-posted field to bind to. Absence is the defence.
// ===========================================================================

/// <summary>What a client may send to create a task.</summary>
public class CreateTaskRequest
{
    /// <summary>Short description of the work. 3-80 characters.</summary>
    [Required(AllowEmptyStrings = false, ErrorMessage = "A task needs a title.")]
    [StringLength(80, MinimumLength = 3, ErrorMessage = "Title must be 3 to 80 characters.")]
    public string Title { get; set; } = "";

    /// <summary>One of: low, normal, high.</summary>
    [AllowedValues("low", "normal", "high", ErrorMessage = "Priority must be low, normal or high.")]
    public string Priority { get; set; } = "normal";

    /// <summary>Optional date the task is due.</summary>
    public DateOnly? DueDate { get; set; }
}

/// <summary>What a client may send to add a comment to a task. Added in 3.2.</summary>
public class AddCommentRequest
{
    [Required(AllowEmptyStrings = false)]
    [StringLength(60, MinimumLength = 1)]
    public string Author { get; set; } = "";

    [Required(AllowEmptyStrings = false)]
    [StringLength(280, MinimumLength = 1)]
    public string Body { get; set; } = "";
}

/// <summary>What a client may send to update a task.</summary>
public class UpdateTaskRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "A task needs a title.")]
    [StringLength(80, MinimumLength = 3, ErrorMessage = "Title must be 3 to 80 characters.")]
    public string Title { get; set; } = "";

    public bool Done { get; set; }

    [AllowedValues("low", "normal", "high", ErrorMessage = "Priority must be low, normal or high.")]
    public string Priority { get; set; } = "normal";

    public DateOnly? DueDate { get; set; }
}

/// <summary>What TaskFlow returns for a task. Note what is NOT here.</summary>
public record TaskResponse(
    int Id,
    string Title,
    bool Done,
    DateOnly? DueDate,
    string Priority,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? CompletedAt);
// no InternalNotes. no IsArchived. They cannot leak from a shape that has no
// room for them.

// ---- mapping: the one place the two worlds are allowed to meet ----
public static class TaskMapping
{
    // The server, not the request body, decides every server-owned field.
    public static TaskItem ToEntity(this CreateTaskRequest request, string createdBy) => new()
    {
        Title = request.Title.Trim(),
        Priority = request.Priority,
        DueDate = request.DueDate,
        Done = false,
        CreatedBy = createdBy,
        InternalNotes = "",
        IsArchived = false,
    };

    // An update may change the client-owned fields only; the rest is carried
    // over from the stored entity.
    public static TaskItem ApplyTo(this UpdateTaskRequest request, TaskItem existing) => existing with
    {
        Title = request.Title.Trim(),
        Done = request.Done,
        Priority = request.Priority,
        DueDate = request.DueDate,
    };

    public static TaskResponse ToResponse(this TaskItem task) => new(
        task.Id, task.Title, task.Done, task.DueDate, task.Priority, task.CreatedAt,
        task.CreatedBy, task.CompletedAt);
}
