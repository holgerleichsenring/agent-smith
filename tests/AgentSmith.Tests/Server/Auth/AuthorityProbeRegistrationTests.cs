using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Services.Startup;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// 2026-09-23-e7f0: the reachability probe is registered, not constructed. It must measure the
/// block the token handler validates against — the same OBJECT the JwtBearer setup was handed,
/// not a second reading of the environment — and it used to get it as a constructor argument.
/// A key carries the same distinction with nothing matched by runtime type.
/// </summary>
public sealed class AuthorityProbeRegistrationTests
{
    [Fact]
    public void Authentication_TheReachabilityProbe_IsRegisteredWithoutActivation()
    {
        var composed = new TokenAuthorityConfig { Authority = "https://authority.example" };
        var services = new ServiceCollection();

        services.AddServerAuthentication(composed);

        var probe = services.Single(d =>
            !d.IsKeyedService && d.ServiceType == typeof(IAuthorityReachability));
        probe.ImplementationType.Should().Be(typeof(AuthorityReachabilityProbe),
            "the container builds the probe from its registration");
        probe.ImplementationFactory.Should().BeNull(
            "a factory is where a constructor argument is matched to a parameter by its type");

        var block = services.Single(d =>
            d.IsKeyedService && d.ServiceType == typeof(TokenAuthorityConfig));
        block.ServiceKey.Should().Be(AuthorityReachabilityProbe.ComposedAuthorityKey);
        block.KeyedImplementationInstance.Should().BeSameAs(composed,
            "an instance keeps the block the handler validates against; a factory would read "
            + "the environment a second time, which is the very thing this avoids");
    }
}
