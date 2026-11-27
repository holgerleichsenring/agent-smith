using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Series;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Series;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;

namespace AgentSmith.Server.Services.Series;

/// <summary>
/// 2026-11-27-3532c: the series master's read scopes for one beat. Every repository of its project at the default branch
/// (<c>&lt;repo&gt;</c>), at the branch of each member ticket a run or a pull request worked (<c>&lt;repo&gt;@&lt;key&gt;</c>,
/// agent-smith/&lt;ticket id&gt;) and at the series branch when it has one (<c>&lt;repo&gt;@series</c>); and its conversation's
/// uploads (<c>reference:&lt;name&gt;</c>) when it came from one. A repository scope names no conversation, so it is the
/// beat's own and never a design conversation's held sandbox; an upload is taken from its conversation's hold and given
/// back, as a design turn does. A project with no repositories has nothing to read; uploads that cannot be listed are
/// left out, logged.
/// </summary>
public sealed class SeriesSourceScopes(
    ISourceScopeSandboxFactory scopes, IReferenceSetSandboxFactory references, IServiceScopeFactory services,
    ILogger<SeriesSourceScopes> logger) : ISeriesSourceScopes
{
    public const string SeriesSuffix = "@series";

    public async Task<SeriesSourceMap> OpenAsync(SeriesBeat beat, ResolvedProject project, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(beat);
        ArgumentNullException.ThrowIfNull(project);
        if (project.Repos.Count == 0) return SeriesSourceMap.None;
        var record = beat.Record;
        // The uploads are listed first: it is the one step that waits on a store, and no scope is open yet.
        var uploaded = await UploadedAsync(record.Conversation, cancellationToken);
        var worked = beat.Facts.Where(f => f.LatestRunId is not null || f.PullRequests.Count > 0).Select(f => f.Ticket).ToList();
        List<SeriesSource> sources = [.. project.Repos.SelectMany(repo => Repository(record, worked, project, repo))];
        sources.AddRange(ReferenceScopeName.For(uploaded.Select(s => s.Name)).Zip(uploaded, (address, set) => new SeriesSource(
            address, $"the upload '{set.Name}'", references.Create(project, record.Conversation, address, set.SetId))));
        logger.LogInformation("Series {Series}: its master may read {Count} scope(s)", record.Id, sources.Count);
        return new SeriesSourceMap(sources, logger);
    }

    private IEnumerable<SeriesSource> Repository(
        SeriesRunRecord record, IReadOnlyList<SeriesTicket> worked, ResolvedProject project, RepoConnection repo)
    {
        yield return new(repo.Name, $"{repo.Name} at its default branch", scopes.Create(project, repo, revision: null, conversationId: null));
        foreach (var ticket in worked)
        {
            var branch = TicketBranchNamer.Compose(new TicketId(ticket.TicketId)).Value;
            yield return new($"{repo.Name}@{ticket.Key}", $"{repo.Name} at {ticket.Key}'s branch {branch}",
                scopes.Create(project, repo, branch, conversationId: null));
        }
        if (record.IntegrationBranch is { } series)
            yield return new(repo.Name + SeriesSuffix, $"{repo.Name} at the series branch {series}",
                scopes.Create(project, repo, series, conversationId: null));
    }

    // A composition with no durable store keeps no uploads; a listing that fails costs the beat its uploads, not its turn.
    private async Task<IReadOnlyList<ReferenceSetSummary>> UploadedAsync(string conversation, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(conversation)) return [];
        try
        {
            await using var scope = services.CreateAsyncScope();
            return scope.ServiceProvider.GetService<ReferenceSetRepository>() is { } sets ? await sets.ListAsync(conversation, ct) : [];
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "The uploads of conversation {Conversation} could not be listed; the master reads none", conversation);
            return [];
        }
    }
}
