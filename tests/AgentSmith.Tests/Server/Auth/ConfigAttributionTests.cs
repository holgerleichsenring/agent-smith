using System.Security.Claims;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Services.Config;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// p0503d: a config change names the principal, or nobody. The <c>X-Actor</c> header this
/// used to read was a client-supplied string written verbatim into the audit trail, and
/// the dashboard never sent it — so every deployment already got the default and the
/// header was forgery surface and nothing else. Deleting the read closes it for every
/// caller at once, which is what "with and without a principal" below is asserting.
/// </summary>
public sealed class ConfigAttributionTests
{
    private const string NameClaim = "sub";
    private const string Caller = "a-caller-the-authority-named";

    [Fact]
    public void Attribution_PrincipalPresent_TheChangeNamesTheConfiguredClaim() =>
        ConfigStudioWriteGuard.Attribution(Context(Authenticated())).Actor.Should().Be(Caller);

    [Fact]
    public void Attribution_XActorHeaderSent_IsIgnoredWithAndWithoutAPrincipal()
    {
        var forged = Context(Authenticated(), actorHeader: "somebody-else");
        var anonymousForged = Context(new ClaimsPrincipal(new ClaimsIdentity()), "somebody-else");

        ConfigStudioWriteGuard.Attribution(forged).Actor.Should().Be(Caller);
        ConfigStudioWriteGuard.Attribution(anonymousForged).Actor.Should().Be("dashboard");
    }

    [Fact]
    public void Attribution_NoPrincipal_KeepsTodaysDefault() =>
        ConfigStudioWriteGuard.Attribution(Context(new ClaimsPrincipal(new ClaimsIdentity())))
            .Actor.Should().Be("dashboard");

    // The feed row and the ConfigChangedEvent are written from ONE attribution, so a
    // replica reacting to the event and an operator reading the audit trail see one name.
    [Fact]
    public async Task Attribution_TheChangedEvent_CarriesTheSameActorAsTheFeed()
    {
        var reload = new Mock<IConfigReloadSignal>();
        reload.Setup(r => r.BumpAsync(It.IsAny<CancellationToken>())).ReturnsAsync(7);
        var events = new Mock<ISystemEventPublisher>();
        var context = Context(Authenticated());

        await ConfigStudioWriteGuard.GuardSignalingAsync(
            context, reload.Object, events.Object, Results.NoContent);

        events.Verify(e => e.PublishAsync(
            It.Is<ConfigChangedEvent>(published =>
                published.Actor == ConfigStudioWriteGuard.Attribution(context).Actor
                && published.Actor == Caller),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static ClaimsPrincipal Authenticated() => new(new ClaimsIdentity(
        [new Claim(NameClaim, Caller)], "Bearer", NameClaim, ClaimTypes.Role));

    private static HttpContext Context(ClaimsPrincipal user, string? actorHeader = null)
    {
        var context = new DefaultHttpContext { User = user };
        if (actorHeader is not null) context.Request.Headers["X-Actor"] = actorHeader;
        return context;
    }
}
