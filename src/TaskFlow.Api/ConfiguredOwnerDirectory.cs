using Microsoft.Extensions.Options;
using TaskFlow.Application;

namespace TaskFlow.Api;

// The OUTER half of IOwnerDirectory: the owner list lives in settings, so adding a team
// is a settings change. TaskService is never edited for it.
public sealed class ConfiguredOwnerDirectory(IOptions<TaskFlowOptions> options) : IOwnerDirectory
{
    private readonly Dictionary<string, string> _known =
        new(options.Value.OwnerEmails, StringComparer.OrdinalIgnoreCase);

    public string EmailFor(string createdBy) =>
        _known.TryGetValue(createdBy, out var email) ? email : options.Value.FallbackOwnerEmail;
}
