using AgentSmith.Contracts.Models.Preflight;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// p0324: aggregates every registered <see cref="IPreflightCheck"/> into one report.
/// Never throws — a crashing check becomes a failed outcome. The same runner backs
/// the CLI `doctor` command (hard exit code) and the server startup run (warn-only,
/// surfaced on /health); there is no second implementation of the checks.
/// </summary>
public interface IPreflightRunner
{
    Task<PreflightReport> RunAsync(CancellationToken cancellationToken);
}
