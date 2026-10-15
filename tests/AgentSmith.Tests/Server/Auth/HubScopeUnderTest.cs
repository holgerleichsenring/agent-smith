using System.Security.Claims;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Hubs;
using AgentSmith.Server.Security;
using AgentSmith.Server.Services.Events;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// 2026-10-15-4a4dc: the hub's project scope built by hand, and a hub invocation of a JobsHub method as
/// SignalR's dispatcher hands it to a filter — the method name, its arguments, the caller and the
/// invocation's own service scope.
/// </summary>
internal static class HubScopeUnderTest
{
    /// <summary>A scope over no store and no configured project — for a case that never reaches a lookup.</summary>
    public static HubProjectScope Scope(TokenAuthorityConfig auth) =>
        new(ProjectScopeUnderTest.Access(auth), Unstored());

    public static RunProjectIndex Unstored() =>
        new(new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>());

    public static HubInvocationContext Invocation(
        ClaimsPrincipal caller, IServiceProvider services, string method, params object?[] arguments) => new(
        new Caller(caller), services, new NoHub(), typeof(JobsHub).GetMethod(method)!, arguments);

    private sealed class NoHub : Hub;

    private sealed class Caller(ClaimsPrincipal user) : HubCallerContext
    {
        public override string ConnectionId => "4a4dc";
        public override string? UserIdentifier => null;
        public override ClaimsPrincipal? User => user;
        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
        public override IFeatureCollection Features { get; } = new FeatureCollection();
        public override CancellationToken ConnectionAborted => CancellationToken.None;
        public override void Abort() { }
    }
}
