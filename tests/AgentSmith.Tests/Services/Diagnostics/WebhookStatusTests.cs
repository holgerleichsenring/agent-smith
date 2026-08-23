using AgentSmith.Server.Services.Diagnostics;
using FluentAssertions;

namespace AgentSmith.Tests.Services.Diagnostics;

/// <summary>
/// p0506: "no secret" and "last seen" already sat two pixels apart on the connections
/// panel; what nothing stated was the conjunction — a delivery landed and nothing
/// verified it. A deployment that never received one says nothing, which is what keeps
/// a polling-only installation from being told to configure a webhook it does not expose.
/// </summary>
public sealed class WebhookStatusTests
{
    [Fact]
    public void WebhookStatus_DeliveredAndNoSecret_SaysItAcceptedSomethingUnsigned()
    {
        var status = new WebhookStatus("github", SecretConfigured: false, DateTimeOffset.UnixEpoch);

        status.AcceptedUnsignedDelivery.Should().BeTrue();
    }

    [Fact]
    public void WebhookStatus_NeverDelivered_SaysNothingAboutExposure()
    {
        var status = new WebhookStatus("github", SecretConfigured: false, LastReceivedUtc: null);

        status.AcceptedUnsignedDelivery.Should().BeFalse();
    }

    [Fact]
    public void WebhookStatus_DeliveredWithASecretConfigured_SaysNothingAboutExposure()
    {
        var status = new WebhookStatus("github", SecretConfigured: true, DateTimeOffset.UnixEpoch);

        status.AcceptedUnsignedDelivery.Should().BeFalse();
    }
}
