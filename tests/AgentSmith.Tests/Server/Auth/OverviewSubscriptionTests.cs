using System.Security.Claims;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Services.Events;
using AgentSmith.Server.Hubs;
using AgentSmith.Server.Security;
using AgentSmith.Server.Services.Events;
using FluentAssertions;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;
using static AgentSmith.Tests.Server.Auth.ProjectBindingTests;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// 2026-10-15-4a4dc: who joins which overview group. A global reader — and everybody while enforcement is
/// off — joins the one overview group and gets the installation's rollup as today; a project-only reader
/// joins the groups of the configured projects they read runs in, and gets no rollup.
/// </summary>
public sealed class OverviewSubscriptionTests
{
    private readonly List<string> _joined = [];
    private readonly List<string> _sent = [];

    [Fact]
    public async Task OverviewSubscription_EnforceOff_JoinsTheOverviewGroupAndPushesTheRollup()
    {
        await JoinAsync(enforce: false, "alpha-team");

        _joined.Should().Equal(HubGroups.Overview);
        _sent.Should().Equal("SystemActivityUpdated");
    }

    [Fact]
    public async Task OverviewSubscription_GlobalReader_JoinsTheOverviewGroupAndPushesTheRollup()
    {
        await JoinAsync(enforce: true, "everyone", "alpha-team");

        _joined.Should().Equal(HubGroups.Overview);
        _sent.Should().Equal("SystemActivityUpdated");
    }

    [Fact]
    public async Task OverviewSubscription_ProjectReader_JoinsOnlyTheirConfiguredProjectGroupsAndGetsNoRollup()
    {
        await JoinAsync(enforce: true, "alpha-team");

        _joined.Should().Equal(HubGroups.OverviewOf("alpha"));
        _sent.Should().BeEmpty("the rollup counts the whole installation's tracker traffic");
    }

    private async Task JoinAsync(bool enforce, params string[] groups)
    {
        var auth = new TokenAuthorityConfig { Enforce = enforce, NameClaim = "preferred_username" };
        var subscription = new OverviewSubscription(
            auth, ResolverUnderTest.Over(auth, new StoredMappingStub(Mapping())),
            ProjectScopeUnderTest.Projects("alpha", "beta"), Broadcaster());
        var groupManager = new Mock<IGroupManager>();
        groupManager.Setup(g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, group, _) => _joined.Add(group)).Returns(Task.CompletedTask);
        var caller = new Mock<ISingleClientProxy>();
        caller.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Callback<string, object?[], CancellationToken>((method, _, _) => _sent.Add(method)).Returns(Task.CompletedTask);

        await subscription.JoinAsync(new Connection(Caller(auth, groups)), groupManager.Object, caller.Object);
    }

    private static RoleMappingConfig Mapping()
    {
        var mapping = ProjectBindingTests.Mapping(
            Binding("alpha", groups: ["alpha-team"], roles: [BuiltInRoles.Operator]),
            Binding("gone", groups: ["alpha-team"], roles: [BuiltInRoles.Operator]));
        mapping.GroupRoles["everyone"] = [BuiltInRoles.Reader];
        return mapping;
    }

    private static ClaimsPrincipal Caller(TokenAuthorityConfig auth, string[] groups) =>
        ResolverUnderTest.Caller(auth, [.. groups.Select(group => ("groups", group)), ("sub", "a-person")]);

    // The rollup is read off the broadcaster, which nothing here starts.
    private static JobsBroadcaster Broadcaster() => new(
        Mock.Of<IConnectionMultiplexer>(), Mock.Of<IRunEventFanout>(), null!,
        NullLogger<JobsBroadcaster>.Instance, new EventEnvelopeSerializer());

    private sealed class Connection(ClaimsPrincipal user) : HubCallerContext
    {
        public override string ConnectionId => "4a4dc-overview";
        public override string? UserIdentifier => null;
        public override ClaimsPrincipal? User => user;
        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
        public override IFeatureCollection Features { get; } = new FeatureCollection();
        public override CancellationToken ConnectionAborted => CancellationToken.None;
        public override void Abort() { }
    }
}
