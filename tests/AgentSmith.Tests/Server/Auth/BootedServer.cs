using AgentSmith.Server.Services.Startup;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// p0503b: boots the REAL composition — the same <see cref="ServerHostFactory.CreateAsync"/>
/// call Program.cs makes — on a loopback port, and hands back a client for it.
/// <para>
/// Parameterised rather than an xUnit fixture on purpose: every case here and in
/// StartupResilienceTests boots against its OWN config file and its OWN environment, which
/// is the whole point of those assertions. A fixture shared across cases cannot serve that,
/// so the helper takes arguments and each case still owns its boot. The environment it sets
/// is restored on disposal, including the dashboard gate, which the server reads from the
/// process on every call.
/// </para>
/// </summary>
public sealed class BootedServer : IAsyncDisposable
{
    /// <summary>Deliberately dead: no case here has a Redis, and none may need one.</summary>
    public const string NoRedis = "127.0.0.1:1";

    private const string DashboardGateVar = "AGENTSMITH_UI_API_ENABLED";

    private readonly WebApplication _app;
    private readonly bool _started;
    private readonly (string Name, string? Value)[] _restore;

    public HttpClient Client { get; }

    public IServiceProvider Services => _app.Services;

    private BootedServer(WebApplication app, bool started, (string, string?)[] restore)
    {
        _app = app;
        _started = started;
        _restore = restore;
        Client = new HttpClient { BaseAddress = new Uri(BaseUrl(app)) };
    }

    public static async Task<BootedServer> StartAsync(
        string configPath, string redisUrl = NoRedis, bool dashboardApi = true)
    {
        var restore = Set(
            ("CONFIG_PATH", configPath),
            ("REDIS_URL", redisUrl),
            (DashboardGateVar, dashboardApi ? "true" : "false"));

        var app = await ServerHostFactory.CreateAsync([]);
        try
        {
            app.Urls.Add("http://127.0.0.1:0");
            await app.StartAsync();
            return new BootedServer(app, started: true, restore);
        }
        catch
        {
            // Host.StopAsync reverses a hosted-service list that is null when StartAsync
            // never completed, which used to REPLACE the real startup exception with an
            // "ArgumentNullException: source" out of the cleanup path.
            await app.DisposeAsync();
            Restore(restore);
            throw;
        }
    }

    private static (string, string?)[] Set(params (string Name, string Value)[] variables)
    {
        var previous = variables
            .Select(v => (v.Name, Environment.GetEnvironmentVariable(v.Name)))
            .ToArray();
        foreach (var (name, value) in variables) Environment.SetEnvironmentVariable(name, value);
        return previous;
    }

    private static void Restore((string Name, string? Value)[] previous)
    {
        foreach (var (name, value) in previous) Environment.SetEnvironmentVariable(name, value);
    }

    private static string BaseUrl(WebApplication app) =>
        app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.First();

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        if (_started) await _app.StopAsync();
        await _app.DisposeAsync();
        Restore(_restore);
    }
}
