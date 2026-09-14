using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-09-13-6f35: the template entries one master pass owns — their addresses, the move
/// that makes them reachable, and the teardown.
/// <para>
/// The entries go into the tool host through <see cref="FilesystemToolHost.AddSandbox"/>,
/// which p0331 built so a mid-run ensure_repo_sandbox escalation could grow the addressable
/// set without touching the pipeline's map. A template is that same move, decided up front
/// — and the map it stays out of is the point: ContextKeys.Sandboxes is iterated by
/// CommitAndPR, the toolchain probe, the preflight checks and the bootstrap handlers, and a
/// foreign read-only checkout in it is a commit attempt on somebody else's repository.
/// </para>
/// <para>
/// Read-only is a property of the SANDBOX and not a promise in the prompt:
/// <c>SourceScopeSandbox</c> refuses Run and WriteFile by step KIND, below the tool host,
/// so the refusal holds whatever the master was told.
/// </para>
/// </summary>
public sealed class MasterTemplateAttachment(
    IReadOnlyDictionary<string, ISourceScopeSandbox> scopes) : IAsyncDisposable
{
    /// <summary>The run that declared no template, or the surface that writes no code.</summary>
    public static MasterTemplateAttachment Empty =>
        new(new Dictionary<string, ISourceScopeSandbox>(StringComparer.Ordinal));

    /// <summary>The addresses the master may read, each one <c>template:&lt;context&gt;</c>.</summary>
    public IReadOnlyList<string> Names { get; } = [.. scopes.Keys];

    /// <summary>
    /// Makes every entry reachable by its own name. The sandbox key AND the repo-name alias
    /// are the same string: a template has no composite toolchain key, because no toolchain
    /// was ever built for it.
    /// </summary>
    public void AttachTo(FilesystemToolHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        foreach (var (name, scope) in scopes) host.AddSandbox(name, name, scope);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var scope in scopes.Values) await scope.DisposeAsync();
    }
}
