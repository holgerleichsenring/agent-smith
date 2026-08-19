using AgentSmith.Application.Services.Events;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Providers.Agent;
using AgentSmith.Server.Extensions;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Tests.ConfigStudio;

/// <summary>
/// p0456: what the capabilities endpoint actually SENDS. The field shape is the studio's
/// instruction for how to edit a value, and it crossed the wire as the .NET enum's member
/// name ("List") while the client contract declares the lowercase word — so every list,
/// flag and map field in the tracker and connection forms rendered as an empty text box
/// over a live value (p0455).
///
/// These assertions read the response BODY of the real route on a real loopback host.
/// Nothing weaker can see this defect: CapabilityFieldKind.List is CapabilityFieldKind.List
/// on both sides of the fix, so an assertion over the descriptor object cannot tell a fixed
/// server from a broken one. Only the characters between the quotes can.
/// </summary>
public sealed class CapabilityWireShapeTests
{
    /// <summary>The four shapes the contract declares, as the client switches on them.</summary>
    private static readonly string[] DeclaredKinds = ["text", "list", "bool", "map"];

    /// <summary>The .NET member names — what leaked before, and what must never appear.</summary>
    private static readonly string[] MemberNames = ["Text", "List", "Bool", "Map"];

    [Fact]
    public async Task Capabilities_FieldKinds_CrossTheWireLowercase()
    {
        var body = await ServedCapabilitiesAsync();

        foreach (var kind in DeclaredKinds)
            body.Should().Contain($"\"kind\":\"{kind}\"", $"the contract declares the shape as '{kind}'");
    }

    [Fact]
    public async Task Capabilities_FieldKinds_NeverCrossTheWireAsEnumMemberNames()
    {
        var body = await ServedCapabilitiesAsync();

        foreach (var name in MemberNames)
            body.Should().NotContain($"\"kind\":\"{name}\"", "a .NET member name is not a wire vocabulary");
    }

    /// <summary>
    /// Serves the REAL route over loopback. The capabilities descriptor is computed per
    /// request from code truth, so it needs the registered chat-client builders and
    /// nothing else — no database, no configuration file. Route mapping still inspects
    /// every studio handler's parameters, so <see cref="IConfigStore"/> must be a known
    /// service type; this host refuses to produce one rather than standing up a fake
    /// store that could quietly answer a request these tests never make.
    /// </summary>
    private static async Task<string> ServedCapabilitiesAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = AppContext.BaseDirectory,
            Args = [],
        });
        builder.Services.AddSingleton<IRunContextAccessor, AsyncLocalRunContextAccessor>();
        builder.Services.AddAgentProviders();
        builder.Services.AddSingleton<IConfigStore>(_ =>
            throw new NotSupportedException("This host serves /api/config/capabilities only."));

        await using var app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0"); // loopback, OS-assigned free port
        app.MapConfigStudioEndpoints();
        await app.StartAsync();

        var baseUrl = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.First();
        using var http = new HttpClient { BaseAddress = new Uri(baseUrl) };
        var body = await http.GetStringAsync("/api/config/capabilities");
        await app.StopAsync();
        return body;
    }
}
