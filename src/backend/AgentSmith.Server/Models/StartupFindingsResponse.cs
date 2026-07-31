using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Server.Models;

/// <summary>
/// p0391a: the answer to "what is wrong with my installation". Degraded is the one flag
/// a banner needs; the counts let a caller decide how loudly to say it.
/// </summary>
public sealed record StartupFindingsResponse(
    bool Degraded,
    int Blocking,
    int Advisory,
    IReadOnlyList<StartupFindingView> Findings)
{
    public static StartupFindingsResponse From(IReadOnlyList<StartupFinding> findings)
    {
        var blocking = findings.Count(f => f.IsBlocking);
        return new StartupFindingsResponse(
            blocking > 0, blocking, findings.Count - blocking,
            findings.Select(StartupFindingView.From).ToList());
    }
}
