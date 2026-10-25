namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// 2026-10-25-c5a7e: the Redis credential one sandbox holds. Disposing it deletes the
/// per-job user; a shared credential belongs to the server and disposes to nothing.
/// </summary>
public sealed class SandboxBusCredential(string url, Func<Task>? release) : IAsyncDisposable
{
    public string Url => url;

    public bool IsPerJob => release is not null;

    public static SandboxBusCredential Shared(string serverUrl) => new(serverUrl, null);

    public async ValueTask DisposeAsync()
    {
        if (release is not null) await release();
    }
}
