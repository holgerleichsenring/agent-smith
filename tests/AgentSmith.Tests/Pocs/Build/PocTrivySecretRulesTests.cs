using AgentSmith.Application.Services.Pocs.Build;
using AgentSmith.Contracts.Pocs;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Pocs.Build;

/// <summary>2026-11-23-933dd: which Trivy secret rules block, by the confidence of the catalog pattern each maps to.</summary>
public sealed class PocTrivySecretRulesTests
{
    private static PocTrivySecretRules Rules(IReadOnlyDictionary<string, int>? confidences = null, string? failure = null)
    {
        var mock = new Mock<IPocSecretConfidences>();
        mock.Setup(c => c.Read()).Returns((confidences ?? PocBuildRig.Confidences(), failure));
        return new PocTrivySecretRules(mock.Object);
    }

    [Fact]
    public void Blocking_AtEight_HoldsAwsPrivateKeyAndStripeSecret_AndNoPublishableKey()
    {
        var (rules, _) = Rules().Blocking(8);

        rules.Should().Contain(["aws-access-key-id", "aws-secret-access-key", "private-key", "stripe-secret-token", "gcp-api-key"])
            .And.NotContain(r => r.Contains("publishable")).And.NotContain("jwt-token");
    }

    [Fact]
    public void Blocking_AtNine_DropsTheGoogleApiKey() => Rules().Blocking(9).Rules.Should().NotContain("gcp-api-key").And.Contain("private-key");

    [Fact]
    public void Blocking_ACatalogWithoutSecrets_IsAFailure() =>
        Rules(new Dictionary<string, int>(), "no secret patterns loaded").Blocking(8).Failure.Should().Contain("no secret patterns");
}
