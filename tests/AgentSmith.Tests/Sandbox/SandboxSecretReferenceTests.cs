using AgentSmith.Application.Services.Sandbox;
using FluentAssertions;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-08-28-b630: one reading of <c>secretName:key</c> serves the pod spec and the
/// preflight report, so a reference the gate calls well-formed is one the pod can carry.
/// </summary>
public sealed class SandboxSecretReferenceTests
{
    [Theory]
    [InlineData("sf-creds:client-id", "sf-creds", "client-id")]
    // A key may itself contain a separator — only the FIRST one splits.
    [InlineData("sf-creds:a:b", "sf-creds", "a:b")]
    public void TryParse_AWellFormedReference_SplitsAtTheFirstSeparator(
        string value, string secret, string key)
    {
        SandboxSecretReference.TryParse(value, out var parsed).Should().BeTrue();

        parsed!.SecretName.Should().Be(secret);
        parsed.Key.Should().Be(key);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no-colon-here")]
    [InlineData(":client-id")]
    [InlineData("sf-creds:")]
    public void TryParse_ABlankSideOrMissingSeparator_IsRefused(string? value)
    {
        SandboxSecretReference.TryParse(value, out var parsed).Should().BeFalse();

        parsed.Should().BeNull();
    }
}
