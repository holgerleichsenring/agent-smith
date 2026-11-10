using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-11-10-4778: what one design turn holds and gives back when it ends — its read scopes (held ones go back to
/// the conversation, a sandbox that served no read holds nothing) and its work clones, which are destroyed, so the
/// next turn's commands start from a new clone.
/// </summary>
public sealed class DesignTurnResources(IReadOnlyDictionary<string, ISandbox> scopes, WorkCloneSet clones) : IAsyncDisposable
{
    public WorkCloneSet Clones => clones;

    /// <summary>The clones first — they hold a model's shell — then each scope; one that throws stops none of the
    /// others, and the first failure is rethrown once all were given back.</summary>
    public async ValueTask DisposeAsync()
    {
        var failures = new List<Exception>();
        try { await clones.DisposeAsync(); }
        catch (Exception ex) { failures.Add(ex); }
        foreach (var scope in scopes.Values)
        {
            try { await scope.DisposeAsync(); }
            catch (Exception ex) { failures.Add(ex); }
        }
        if (failures.Count > 0) throw new AggregateException("A design turn's resources were not all given back.", failures);
    }
}

/// <summary>2026-11-10-4778: builds a turn's <see cref="DesignTurnResources"/> over the opener the clones use.</summary>
public sealed class DesignTurnResourcesFactory(WorkCloneOpener opener)
{
    public DesignTurnResources For(
        ResolvedProject project, IReadOnlyList<RepoConnection> repos, IReadOnlyDictionary<string, ISandbox> scopes) =>
        new(scopes, new WorkCloneSet(opener, project, repos));
}
