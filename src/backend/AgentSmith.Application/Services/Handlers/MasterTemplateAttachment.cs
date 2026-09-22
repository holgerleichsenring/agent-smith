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
/// <c>SourceScopeSandbox</c> enforces it below the tool host, so it holds whatever the
/// master was told. 2026-09-22-46ef: it is no longer enforced by step KIND. A process is
/// served only when its program is one the server itself builds, so <c>run_command</c> —
/// which travels as a shell with a command string — is refused against a template; and a
/// write is served only under a prefix the scope declares, of which a template declares
/// none. The master's file search IS served against a template, which is why the operand
/// rule is the other half: its root reaches <c>find</c> as a rooted path and never as the
/// start of an expression.
/// </para>
/// </summary>
public sealed class MasterTemplateAttachment(
    IReadOnlyDictionary<string, ISourceScopeSandbox> scopes) : IAsyncDisposable
{
    /// <summary>The run that declared no template, or the surface that writes no code.</summary>
    public static MasterTemplateAttachment Empty =>
        new(new Dictionary<string, ISourceScopeSandbox>(StringComparer.Ordinal));

    /// <summary>The addresses the master may read, each built by <see cref="Specs.TemplateScopeName"/>.</summary>
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
