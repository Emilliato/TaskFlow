using System.ComponentModel.DataAnnotations;
using System.Data.Common;
using Microsoft.Extensions.Options;

namespace TaskFlow.Api;

/// <summary>Everything TaskFlow reads from configuration, bound from the "TaskFlow" section.</summary>
public sealed class TaskFlowOptions
{
    public const string SectionName = "TaskFlow";

    [Required]
    public string ConnectionString { get; set; } = "";

    [RegularExpression("^(Sqlite|InMemory)$", ErrorMessage = "Storage must be Sqlite or InMemory.")]
    public string Storage { get; set; } = "Sqlite";

    [Range(1, 100)]
    public int DefaultPageSize { get; set; }

    // A secret: it has no value in any file that is committed. [Required] makes a missing one a startup failure.
    [Required, MinLength(16)]
    public string AdminApiKey { get; set; } = "";
}

/// <summary>The rule an attribute cannot express: with SQLite, the folder that holds the file must exist.</summary>
public sealed class TaskFlowOptionsValidator : IValidateOptions<TaskFlowOptions>
{
    public ValidateOptionsResult Validate(string? name, TaskFlowOptions options)
    {
        if (!options.Storage.Equals("Sqlite", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(options.ConnectionString))
            return ValidateOptionsResult.Success;

        var connection = new DbConnectionStringBuilder { ConnectionString = options.ConnectionString };
        if (connection.TryGetValue("Data Source", out var source) && source is string file)
        {
            var folder = Path.GetDirectoryName(Path.GetFullPath(file));
            if (!Directory.Exists(folder))
                return ValidateOptionsResult.Fail(
                    $"TaskFlow:ConnectionString points into a folder that does not exist: {folder}");
        }

        return ValidateOptionsResult.Success;
    }
}
