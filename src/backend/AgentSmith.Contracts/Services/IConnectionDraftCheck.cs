using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-10-02-5f89b: checks one connection type against its host — host, identity, scope, repos —
/// yielding a step at a time. The caller stops enumerating at the first failing step, so no call
/// after a failure is ever made.
/// </summary>
public interface IConnectionDraftCheck
{
    RepoType Type { get; }

    IAsyncEnumerable<DraftCheckStep> RunAsync(
        ResolvedConnection connection, string token, CancellationToken cancellationToken);
}
