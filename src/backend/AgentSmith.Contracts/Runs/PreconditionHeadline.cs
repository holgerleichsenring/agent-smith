using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.Runs;

/// <summary>
/// 2026-11-16-f3fbb: the headline of a precondition stop — what it is about and the setting that lifts it,
/// worded as the ticket page's detail sentence words it, without the closing period. Without a detail the
/// kind's own words stand.
/// </summary>
public static class PreconditionHeadline
{
    public static string Of(string? kind, EndDetail? detail)
    {
        var lever = detail is { Control: not null, SetTo: not null } ? $": set {detail.Control} to {detail.SetTo}" : "";
        return (kind, detail) switch
        {
            (RunEndKinds.ImageUntrusted, not null) => $"{detail.Subject} is not trusted{lever}",
            (RunEndKinds.ImageUntrusted, null) => "a context names a container image outside the trusted registries",
            (RunEndKinds.BootstrapMissing, { Repos.Count: > 0 }) => Bootstrap(detail),
            (RunEndKinds.BootstrapMissing, _) => "a repository has no agent-smith bootstrap",
            (_, not null) => $"{detail.Subject}{lever}",
            _ => "a precondition did not hold",
        };
    }

    private static string Bootstrap(EndDetail detail)
    {
        var lift = detail is { Control: not null, SetTo: not null } ? $": {detail.Control} project {detail.SetTo}" : "";
        var verb = detail.Repos.Count == 1 ? "has" : "have";
        return $"{string.Join(", ", detail.Repos)} {verb} no agent-smith bootstrap{lift}";
    }
}
