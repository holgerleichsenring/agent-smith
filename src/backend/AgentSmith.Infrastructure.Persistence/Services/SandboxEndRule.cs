using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// 2026-10-13-9a59i: the end reason each close event and each run end names. A vanish is the
/// infrastructure's verdict; a dispose says what the backend read before the release; the run's end
/// names a release — park, requeue or end.
/// </summary>
public static class SandboxEndRule
{
    public static string OfVanish(SandboxVanishedEvent e) =>
        e.OomKilled == true ? SandboxEndReasons.OomKilled : SandboxEndReasons.Vanished;

    public static string OfDispose(SandboxDisposedEvent e) => e.ExitState switch
    {
        SandboxExitLabels.Exited => e.OomKilled == true ? SandboxEndReasons.OomKilled : SandboxEndReasons.Exited,
        SandboxExitLabels.Gone => SandboxEndReasons.Gone,
        _ => SandboxEndReasons.Released,
    };

    /// <summary>A queued RunFinished is a capacity rejection, not a wait on a person.</summary>
    public static string OfRelease(string runStatus) => runStatus switch
    {
        RunStatuses.WaitingForInput => SandboxEndReasons.ReleasedAtPark,
        RunStatuses.Queued => SandboxEndReasons.ReleasedAtRequeue,
        _ => SandboxEndReasons.ReleasedAtEnd,
    };
}
