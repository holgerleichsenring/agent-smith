using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgentSmith.PipelineHarness.Composition;

/// <summary>
/// p0199f: per-test Kestrel mini-server backing the api-security-scan
/// docker-tier passive-mode fixture. Serves the StubApiTarget/openapi.json
/// content at /openapi.json and a trivial /health endpoint, bound to an
/// ephemeral loopback port so two parallel test runs never collide. The
/// scanner containers (when env-gated AGENTSMITH_HARNESS_REAL_SCANNERS=1)
/// reach this server via host.docker.internal:{Port}; the default test
/// path stubs the scanners and never needs the URL to be reachable from
/// inside the sandbox, but ApiTarget + SwaggerPath still point at it so
/// the pipeline context carries a real URL instead of a fabricated one.
/// </summary>
public sealed class StubApiTargetHost : IAsyncDisposable
{
    private readonly WebApplication _app;

    public int Port { get; }
    public string BaseUrl => $"http://host.docker.internal:{Port}";
    public string LoopbackUrl => $"http://127.0.0.1:{Port}";
    public string OpenApiUrl => $"{BaseUrl}/openapi.json";

    private StubApiTargetHost(WebApplication app, int port)
    {
        _app = app;
        Port = port;
    }

    public static async Task<StubApiTargetHost> StartAsync(string openApiJsonPath)
    {
        var json = await File.ReadAllTextAsync(openApiJsonPath);
        var app = BuildApp(json);
        await app.StartAsync();
        return new StubApiTargetHost(app, ResolvePort(app));
    }

    private static WebApplication BuildApp(string openApiJson)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton(new OpenApiPayload(openApiJson));
        var app = builder.Build();
        app.MapGet("/openapi.json", (OpenApiPayload payload) =>
            Results.Text(payload.Json, "application/json"));
        app.MapGet("/health", () => Results.Json(new { status = "ok" }));
        return app;
    }

    private static int ResolvePort(WebApplication app)
    {
        var addresses = app.Services
            .GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>()
            .Features.Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>()
            ?? throw new InvalidOperationException("Kestrel did not surface IServerAddressesFeature.");
        var url = addresses.Addresses.FirstOrDefault()
            ?? throw new InvalidOperationException("Kestrel started without an address.");
        return new Uri(url).Port;
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }

    private sealed record OpenApiPayload(string Json);
}
