using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// p0503e: an authority whose discovery document can be taken away and given back without
/// its address changing. A stopped loopback listener cannot be brought back on the same
/// port, so the outage-AND-recovery the probe exists for needs one endpoint that answers
/// or does not, on command.
/// </summary>
public sealed class FlakyAuthority : IAsyncDisposable
{
    private readonly WebApplication _app;

    /// <summary>Set false to take the discovery document away.</summary>
    public bool Serving { get; set; } = true;

    public string Authority { get; private set; } = string.Empty;

    private FlakyAuthority(WebApplication app) => _app = app;

    public static async Task<FlakyAuthority> StartAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        var app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");

        var authority = new FlakyAuthority(app);
        app.MapGet("/.well-known/openid-configuration", () => authority.Serving
            ? Results.Json(new { issuer = authority.Authority })
            : Results.StatusCode(StatusCodes.Status503ServiceUnavailable));

        await app.StartAsync();
        authority.Authority = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.First().TrimEnd('/');
        return authority;
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
