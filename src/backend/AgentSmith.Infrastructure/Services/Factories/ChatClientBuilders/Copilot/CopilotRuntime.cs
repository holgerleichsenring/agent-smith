using AgentSmith.Contracts.Constants;
using GitHub.Copilot;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders.Copilot;

/// <summary>
/// 2026-09-07-d5f2: owns the one Copilot runtime process for the container.
///
/// The process starts LAZILY. ConfigCapabilitiesTests resolves every registered IChatClientBuilder
/// from a real ServiceProvider and CI has no runtime binary, so starting in the constructor would
/// turn a capability assertion into an integration test. The postures the client and its sessions
/// are opened with live in <see cref="CopilotSessionFactory"/>.
/// </summary>
public sealed class CopilotRuntime : ICopilotRuntime, IAsyncDisposable, IDisposable
{
    private readonly ILogger<CopilotRuntime> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly SemaphoreSlim _startGate = new(1, 1);
    private CopilotClient? _client;

    public CopilotRuntime(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<CopilotRuntime>();
    }
    public async Task<ICopilotSessionHandle> CreateSessionAsync(
        CopilotSessionRequest request, CancellationToken cancellationToken)
    {
        var client = await EnsureStartedAsync(cancellationToken);
        var config = CopilotSessionFactory.BuildSessionConfig(request);

        var session = await client.CreateSessionAsync(config, cancellationToken);
        _logger.LogDebug("Opened Copilot session {Session} for model {Model}.", session.SessionId, request.Model);
        return new CopilotSessionHandle(session, _loggerFactory.CreateLogger<CopilotSessionHandle>());
    }

    public async Task DeleteSessionAsync(string sessionId, CancellationToken cancellationToken)
    {
        if (_client is null) return;
        try
        {
            await _client.DeleteSessionAsync(sessionId, cancellationToken);
        }
        catch (Exception ex)
        {
            // Costs disk, not correctness — and the call that triggered the rebuild must still succeed.
            _logger.LogWarning(ex, "Could not delete abandoned Copilot session {Session}.", sessionId);
        }
    }

    /// <summary>Whether the runtime process has been started. False until the first session.</summary>
    internal bool IsStarted => _client is not null;

    private async Task<CopilotClient> EnsureStartedAsync(CancellationToken cancellationToken)
    {
        if (_client is not null) return _client;
        await _startGate.WaitAsync(cancellationToken);
        try
        {
            if (_client is not null) return _client;

            var runtimePath = Environment.GetEnvironmentVariable(AgentEnvKeys.CopilotCliPath);
            var options = CopilotSessionFactory.BuildClientOptions(runtimePath, ResolveBaseDirectory());
            options.Logger = _logger;

            var client = new CopilotClient(options);
            await client.StartAsync(cancellationToken);
            _client = client;
            _logger.LogInformation("Copilot runtime started ({Runtime}).", runtimePath ?? "bundled");
            return client;
        }
        finally
        {
            _startGate.Release();
        }
    }

    /// <summary>Empty mode needs somewhere for session state; the agent's own home, so a
    /// sandbox's transcripts go away with the sandbox.</summary>
    private static string ResolveBaseDirectory()
    {
        var home = Environment.GetEnvironmentVariable("HOME")
            ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Directory.CreateDirectory(Path.Combine(home, ".agentsmith", "copilot")).FullName;
    }

    public async ValueTask DisposeAsync()
    {
        if (_client is null) return;
        try
        {
            await _client.StopAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The Copilot runtime did not stop cleanly.");
        }
        finally
        {
            await _client.DisposeAsync();
            _client = null;
            _startGate.Dispose();
        }
    }

    /// <summary>
    /// Both disposal shapes exist because a singleton implementing ONLY IAsyncDisposable makes a
    /// synchronous ServiceProvider.Dispose() throw. The synchronous path does not await a clean
    /// stop: a caller who disposed synchronously has already said it will not wait.
    /// </summary>
    public void Dispose()
    {
        if (_client is null) return;
        _client.Dispose();
        _client = null;
        _startGate.Dispose();
    }
}
