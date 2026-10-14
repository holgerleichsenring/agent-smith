using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Core.Services;

/// <summary>
/// 2026-10-13-8762p: the repository copy of a decided default, one TradeOff entry in the spec's or the run's
/// decision file, written once. It holds its own <see cref="RepositoryDecisionFile"/>, so its lock is not the
/// decision logger's: the record step that calls it runs after the spec's master under the ticket's one lease.
/// </summary>
public sealed class RepositoryVerdictFile : IDefaultVerdictFile
{
    private readonly RepositoryDecisionFile _file;

    public RepositoryVerdictFile(ILogger<RepositoryVerdictFile> logger) => _file = new RepositoryDecisionFile(logger);

    public async Task<string?> AppendAsync(
        ISandboxFileReader files, string fileId, string marker, string entry, CancellationToken cancellationToken)
    {
        if (DecisionFileLabel.Resolve(fileId, fileId) is not { } label) return null;
        return await _file.AppendAsync(files, label, DecisionCategory.TradeOff, entry, cancellationToken, unlessPresent: marker)
            ? RepositoryDecisionFile.PathOf(label)
            : null;
    }
}
