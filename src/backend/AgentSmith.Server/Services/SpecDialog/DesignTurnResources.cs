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

    public async ValueTask DisposeAsync()
    {
        foreach (var scope in scopes.Values) await scope.DisposeAsync();
        await clones.DisposeAsync();
    }
}

/// <summary>2026-11-10-4778: builds a turn's <see cref="DesignTurnResources"/> over the opener the clones use.</summary>
public sealed class DesignTurnResourcesFactory(WorkCloneOpener opener)
{
    public DesignTurnResources For(
        ResolvedProject project, IReadOnlyList<RepoConnection> repos, IReadOnlyDictionary<string, ISandbox> scopes) =>
        new(scopes, new WorkCloneSet(opener, project, repos));
}
