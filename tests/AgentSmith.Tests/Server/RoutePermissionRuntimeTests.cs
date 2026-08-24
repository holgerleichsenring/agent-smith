using System.Net;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Security;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Tests.Server;

/// <summary>
/// p0503a: the declaration changes NOTHING at runtime, proven on a booted host with no
/// authorization middleware — the exact condition under which RequireAuthorization would
/// have turned sixty-three routes into 500s. A metadata type the framework does not
/// inspect is the whole point, and a claim of inertness that is only argued is worth
/// nothing next to one a request answers.
/// </summary>
public sealed class RoutePermissionRuntimeTests
{
    [Fact]
    public async Task Runtime_AnnotatedRouteOnABootedHost_AnswersInsteadOfFivehundred()
    {
        await using var app = await StartAsync(host =>
        {
            host.MapGet("/needs", () => Results.Ok("declared")).Needs(Permissions.RunsRead);
            host.MapGet("/open", () => Results.Ok("anonymous")).Anonymous("a probe cannot authenticate");
        });
        using var client = NewClient(app);

        var declared = await client.GetAsync("/needs");
        var open = await client.GetAsync("/open");

        declared.StatusCode.Should().Be(HttpStatusCode.OK);
        open.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Runtime_TheHubMapCall_StillNegotiates()
    {
        await using var app = await StartAsync(host => host.MapHub<SilentHub>("/hub/test")
            .Needs(Permissions.RunsRead));
        using var client = NewClient(app);

        var negotiate = await client.PostAsync("/hub/test/negotiate?negotiateVersion=1", null);

        negotiate.StatusCode.Should().Be(HttpStatusCode.OK);
        (await negotiate.Content.ReadAsStringAsync()).Should().Contain("connectionId");
    }

    private static async Task<WebApplication> StartAsync(Action<WebApplication> map)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = AppContext.BaseDirectory,
            Args = [],
        });
        builder.Services.AddSignalR();
        var app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0"); // loopback, OS-assigned free port
        map(app);
        await app.StartAsync();
        return app;
    }

    private static HttpClient NewClient(WebApplication app) => new()
    {
        BaseAddress = new Uri(app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.First()),
    };

    private sealed class SilentHub : Hub;
}
