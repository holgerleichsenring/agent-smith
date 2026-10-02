using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Services;
using FluentAssertions;

namespace AgentSmith.Tests.ConfigStudio;

/// <summary>
/// 2026-10-02-140d: the descriptor says authSecret holds a secret NAME. Declared as text, the
/// form rendered it as a free text box beside its own fixed secret picker — the field twice,
/// once inviting the token value itself.
/// </summary>
public sealed class CapabilitySecretFieldTests
{
    [Fact]
    public void Capabilities_AuthSecret_IsDeclaredASecretOnEveryTrackerAndConnectionType()
    {
        var capabilities = ConfigStudioCapabilities.Build(["claude"]);

        var fields = capabilities.TrackerTypes.Select(t => t.Fields)
            .Concat(capabilities.ConnectionTypes.Select(c => c.Fields))
            .ToList();

        fields.Should().NotBeEmpty();
        foreach (var set in fields)
        {
            var authSecret = set.Should().ContainSingle(f => f.Key == "authSecret").Which;
            authSecret.Kind.Should().Be(CapabilityFieldKind.Secret);
            authSecret.Required.Should().BeTrue();
        }
    }
}
