using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Memory;

/// <summary>
/// 2026-10-25-9324a: the ledger key of a run's repository — <see cref="RepoKey.Of"/> of its URL in the resolved
/// project, else in the run's repository list, so two projects naming one repository differently share its
/// record. A repository configured by path only has no URL, and is keyed by its name.
/// </summary>
public sealed class MemoryRepositoryKey
{
    public string Of(PipelineContext pipeline, string repository)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        var url = UrlIn(pipeline.TryGet<ResolvedProject>(ContextKeys.ProjectConfig, out var project) ? project?.Repos : null, repository)
            ?? UrlIn(pipeline.TryGet<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, out var repos) ? repos : null, repository);
        return RepoKey.Of(url ?? repository);
    }

    private static string? UrlIn(IReadOnlyList<RepoConnection>? repos, string repository) =>
        repos?.FirstOrDefault(r => string.Equals(r.Name, repository, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(r.Url))?.Url;
}
