using System.Security.Claims;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Security;
using FluentAssertions;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// p0517: the branch the running server cannot reach. Every hub method IS in the table —
/// p0503c's enumeration test makes a gap a build failure — so a method the table does not
/// name can only be produced by building the invocation context by hand. That is exactly
/// why the branch has to fail closed: the day a twelfth method arrives without an entry,
/// the cost is a refused invocation rather than a silent hole.
/// </summary>
public sealed class HubPermissionFilterTests
{
    [Fact]
    public async Task Enforce_SwitchOn_MethodMissingFromTheTable_IsRefusedRatherThanAllowed()
    {
        var invoked = false;

        var refusal = await Record.ExceptionAsync(() =>
            Filter(enforce: true).InvokeMethodAsync(
                Unclassified(),
                _ => { invoked = true; return ValueTask.FromResult<object?>(null); }).AsTask());

        refusal.Should().BeOfType<HubException>()
            .Which.Message.Should().Contain(nameof(HubMethodPermissions));
        invoked.Should().BeFalse("an unclassified method is refused, not waved through");
    }

    [Fact]
    public async Task Enforce_SwitchOff_AnUnclassifiedMethodIsNotRefusedEither()
    {
        var invoked = false;

        await Filter(enforce: false).InvokeMethodAsync(
            Unclassified(),
            _ => { invoked = true; return ValueTask.FromResult<object?>(null); });

        invoked.Should().BeTrue("the switch is what refuses, and it is off");
    }

    private static HubPermissionFilter Filter(bool enforce)
    {
        var auth = new TokenAuthorityConfig { Authority = "https://an-authority", Enforce = enforce };
        return new HubPermissionFilter(auth, ResolverUnderTest.With(auth));
    }

    // A hub whose one method no table entry names. Its own type is irrelevant: the filter
    // reads the method NAME the dispatcher would report, and nothing else.
    private static HubInvocationContext Unclassified() => new(
        new AnonymousCallerContext(), new EmptyServices(), new UnclassifiedHub(),
        typeof(UnclassifiedHub).GetMethod(nameof(UnclassifiedHub.NobodyClassifiedThis))!,
        []);

    private sealed class UnclassifiedHub : Hub
    {
        public Task NobodyClassifiedThis() => Task.CompletedTask;
    }

    private sealed class EmptyServices : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private sealed class AnonymousCallerContext : HubCallerContext
    {
        public override string ConnectionId => "p0517";
        public override string? UserIdentifier => null;
        public override ClaimsPrincipal? User => null;
        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
        public override IFeatureCollection Features { get; } = new FeatureCollection();
        public override CancellationToken ConnectionAborted => CancellationToken.None;
        public override void Abort() { }
    }
}
