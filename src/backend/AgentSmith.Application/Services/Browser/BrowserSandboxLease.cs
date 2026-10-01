using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Browser;

/// <summary>
/// 2026-10-01-283de: one render's use of the conversation's browser sandbox. Disposing it hands
/// the sandbox back to the hold register for the next render — the next call of this turn or of a
/// later one — and disposes it only where this backend cannot hold one.
/// </summary>
public sealed class BrowserSandboxLease(ISandbox sandbox, SourceScopeHold hold) : IAsyncDisposable
{
    public ISandbox Sandbox => sandbox;

    public async ValueTask DisposeAsync()
    {
        if (!hold.Keep(sandbox)) await sandbox.DisposeAsync();
    }
}
