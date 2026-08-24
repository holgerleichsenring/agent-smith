using AgentSmith.Server.Hubs;
using AgentSmith.Server.Security;
using FluentAssertions;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// p0517: the teeth, on the real hub. One booted server with the enforce switch on, and
/// invocations driven through SignalR's own dispatcher over long polling — because the
/// claim under test is that the dispatcher names a method the way the table does, and an
/// argued claim is worth nothing next to one an invocation answers.
/// </summary>
[Collection(TestSupport.EnvVarCollection.Name)]
public sealed class HubEnforcementTests(EnforcingAuthorityFixture fixture)
    : IClassFixture<EnforcingAuthorityFixture>
{
    private const string HubPath = "/hub/jobs";

    [Fact]
    public async Task Enforce_SwitchOn_CallerLacksThePermission_TheInvocationIsRefused()
    {
        var completion = await Hub(Permissions.RunsRead).InvokeAsync(
            nameof(JobsHub.ExpandSandbox), "run-1", "a-repository");

        completion.Should().Contain("\"error\"")
            .And.Contain(Permissions.RunsWatch, "the refusal names what was missing");
    }

    [Fact]
    public async Task Enforce_SwitchOn_CallerHoldsThePermission_TheInvocationProceeds()
    {
        // SubscribeOverview joins a group and pushes the current rollup — no Redis, no
        // database, so a completion carrying no error means the method really ran.
        var completion = await Hub(Permissions.RunsRead)
            .InvokeAsync(nameof(JobsHub.SubscribeOverview));

        completion.Should().NotContain("\"error\"");
    }

    [Fact]
    public async Task Enforce_SwitchOn_TheWatchPermission_LetsAReaderExpandASandbox()
    {
        var completion = await Hub(Permissions.RunsRead, Permissions.RunsWatch)
            .InvokeAsync(nameof(JobsHub.ExpandSandbox), "run-2", "a-repository");

        completion.Should().NotContain("\"error\"",
            "the sandbox drawer is most of what watching a run is for");
    }

    private HubLongPoll Hub(params string[] permissions) => new(
        fixture.Server.Client, HubPath,
        fixture.Issuer.Token(AuthorityFixture.Audience, permissions));
}
