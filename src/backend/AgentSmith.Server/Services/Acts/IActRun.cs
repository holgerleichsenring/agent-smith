using AgentSmith.Domain.Models;

namespace AgentSmith.Server.Services.Acts;

/// <summary>
/// 2026-11-17-e061a: the one entry through which the server starts a run that acts for a person — a filing, a close, a
/// reopen — seeded with what the act needs and a slot its step fills. The run has a run id, steps, events and a
/// ledger; the server accepts the act and reads the slot back.
/// </summary>
public interface IActRun
{
    /// <summary>Runs <paramref name="pipeline"/> for <paramref name="project"/> on no token and answers how it ended;
    /// a run that could not start answers a failed result, never a throw.</summary>
    Task<CommandResult> RunAsync(string project, string pipeline, IReadOnlyDictionary<string, object> seeds);
}
