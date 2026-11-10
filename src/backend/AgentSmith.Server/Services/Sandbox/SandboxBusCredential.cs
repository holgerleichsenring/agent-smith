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

    /// <summary>2026-11-10-4778: refuses a shared credential for a spawn that requires its own.</summary>
    public SandboxBusCredential Require(bool perJob) => !perJob || IsPerJob ? this
        : throw new InvalidOperationException(
            "This sandbox needs its own bus credential, and the bus issued the shared one (Redis refused ACL SETUSER), " +
            "so it is not started: a shell in it could reach other jobs' channels.");

    public async ValueTask DisposeAsync()
    {
        if (release is not null) await release();
    }
}
