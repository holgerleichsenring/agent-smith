using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Deployment;

/// <summary>
/// 2026-11-18-3863c: the deploy pull request's CI as the host reports it when the run ends, read through the CI loop's
/// own reader (2026-11-17-3435a) — none, pending, green, or red with the failed checks and their links. A host without
/// the reader, or a read that fails, says so; neither fails the deploy.
/// </summary>
public sealed class DeployCiStatus(ILogger<DeployCiStatus> logger)
{
    public async Task<string> ReadAsync(
        ISourceProvider provider, string prUrl, CiReadRequest bounds, CancellationToken cancellationToken)
    {
        if (provider is not IPullRequestCiReader reader) return "its CI is not readable on this host";
        try
        {
            var ci = await reader.ReadCiAsync(prUrl, bounds, cancellationToken);
            return ci.State == CiStates.Red
                ? "CI red: " + string.Join("; ", ci.Failures.Select(f => f.Url is null ? f.Name : $"{f.Name} ({f.Url})"))
                : $"CI {ci.State}";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "The CI of {Url} could not be read", prUrl);
            return $"its CI could not be read ({ex.Message})";
        }
    }
}
