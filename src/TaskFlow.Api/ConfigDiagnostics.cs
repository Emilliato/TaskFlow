namespace TaskFlow.Api;

/// <summary>
/// Prints, for every key under the requested sections, the value that won and the configuration
/// provider that supplied it. Secret-looking values are masked.
/// </summary>
public static class ConfigDiagnostics
{
    private static readonly string[] Secretive = ["ApiKey", "Password", "Secret", "Token"];

    // dotnet TaskFlow.Api.dll --ShowConfig=TaskFlow         (several sections: --ShowConfig=TaskFlow,Logging)
    public static bool TryPrint(IConfigurationRoot configuration)
    {
        var requested = configuration["ShowConfig"];
        if (string.IsNullOrWhiteSpace(requested)) return false;

        var sections = requested.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var view = configuration.GetDebugView(context =>
            Secretive.Any(word => context.Key.Contains(word, StringComparison.OrdinalIgnoreCase))
                ? "********"
                : context.Value ?? string.Empty);

        // The debug view is one block per top-level section: an unindented "Name:" line, then its keys.
        var show = false;
        foreach (var line in view.Split('\n').Select(l => l.TrimEnd('\r')))
        {
            if (line.Length > 0 && !char.IsWhiteSpace(line[0]))
                show = sections.Contains(line.TrimEnd(':'), StringComparer.OrdinalIgnoreCase);
            if (show) Console.WriteLine(line);
        }

        return true;
    }
}
