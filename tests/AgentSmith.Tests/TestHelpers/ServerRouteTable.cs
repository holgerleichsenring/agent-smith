using AgentSmith.Server.Extensions;
using AgentSmith.Server.Security;
using AgentSmith.Server.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Tests.TestHelpers;

/// <summary>
/// p0506: enumerates the routes a mapping actually produces, over the REAL server
/// composition and without resolving a single service — so no database, no Redis and no
/// configuration are needed to ask "is this route mapped?".
/// <para>
/// Two things here are not the obvious spelling, and both were measured rather than
/// reasoned about. <c>app.Services.GetRequiredService&lt;EndpointDataSource&gt;()</c>
/// returns EMPTY before UseRouting runs; the enumerable seam is
/// <c>((IEndpointRouteBuilder)app).DataSources</c>. And enumerating it runs
/// RequestDelegateFactory.InferMetadata, which THROWS ("Did you mean to register the
/// UNKNOWN parameters as a Service?") unless every handler parameter type is REGISTERED
/// — registration alone suffices, because a factory that would throw on resolve is never
/// invoked during enumeration.
/// </para>
/// <para>
/// p0503a reads the same endpoints for what they DECLARE (<see cref="Facts"/>): the
/// permission metadata rides on the endpoint, so the route table and the permission
/// table are one enumeration, not two lists that can drift apart.
/// </para>
/// </summary>
internal static class ServerRouteTable
{
    public static IReadOnlyList<string> Patterns(
        Action<WebApplication> map, Action<IServiceCollection>? configureServices = null) =>
        [.. Facts(map, configureServices).Select(fact => fact.Pattern)];

    public static IReadOnlyList<RouteFact> Facts(
        Action<WebApplication> map, Action<IServiceCollection>? configureServices = null)
    {
        var builder = WebApplication.CreateBuilder();
        ServerCompositionBuilder.ConfigureServices(builder.Services, "agentsmith.yml");
        builder.Services.AddDashboardApi();
        configureServices?.Invoke(builder.Services);
        var app = builder.Build();
        map(app);
        return [.. ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(Read)];
    }

    private static RouteFact Read(RouteEndpoint endpoint) => new(
        string.Join(
            ",", endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["*"]),
        endpoint.RoutePattern.RawText ?? string.Empty,
        endpoint.Metadata.GetMetadata<RequiresPermission>()?.Names ?? [],
        endpoint.Metadata.GetMetadata<AnonymousRoute>()?.Reason);
}
