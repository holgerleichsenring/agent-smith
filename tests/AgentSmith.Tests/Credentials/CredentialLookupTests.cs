using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using FluentAssertions;

namespace AgentSmith.Tests.Credentials;

/// <summary>
/// 2026-11-18-78efb: a named credential's lookup tells a value, none and a stored value that cannot be opened apart —
/// by its state, never by an empty string — and its text never carries the value.
/// </summary>
public sealed class CredentialLookupTests
{
    private const string Value = "lookup-value-0123456789";

    [Fact]
    public void Lookup_TheEnvironment_ThenTheCatalog_ThenUnreadable_ThenNone()
    {
        var catalog = new LoadedSecretValues(new AgentSmithConfig
        {
            Secrets = new Dictionary<string, string> { ["stored"] = Value, ["broken"] = string.Empty, ["empty"] = string.Empty },
            UnreadableSecrets = new Dictionary<string, string> { ["broken"] = "no master key is configured (key env:1)" },
        });

        EnvironmentFirst.Lookup("BROKEN", name => name == "BROKEN" ? "from-env" : null, catalog).Value.Should().Be("from-env");
        EnvironmentFirst.Lookup("STORED", _ => null, catalog).Value.Should().Be(Value);
        var broken = EnvironmentFirst.Lookup("BROKEN", _ => null, catalog);
        broken.State.Should().Be(CredentialLookupState.Unreadable);
        broken.Secret.Should().Be("BROKEN");
        broken.Reason.Should().Contain("no master key");
        EnvironmentFirst.Lookup("EMPTY", _ => null, catalog).State.Should().Be(CredentialLookupState.None);
        EnvironmentFirst.Resolve("BROKEN", _ => null, catalog).Should().BeNull();
    }

    [Fact]
    public void Text_NeverCarriesTheValue()
    {
        CredentialLookup.Of(Value).ToString().Should().NotContain(Value);
        CredentialLookup.Of(string.Empty).State.Should().Be(CredentialLookupState.None);
        new StoredSecretLoad(new Dictionary<string, string> { ["a"] = Value }, new Dictionary<string, string>())
            .ToString().Should().NotContain(Value);
    }

    [Fact]
    public void CredentialResolver_Lookup_SaysUnreadable_AndNamedStaysNull()
    {
        var resolver = new CredentialResolver(new LoadedSecretValues(new AgentSmithConfig
        {
            UnreadableSecrets = new Dictionary<string, string> { ["SLACK_SIGNING_SECRET"] = "tampered (key env:1)" },
        }), _ => null);

        resolver.Lookup("SLACK_SIGNING_SECRET").State.Should().Be(CredentialLookupState.Unreadable);
        resolver.Named("SLACK_SIGNING_SECRET").Should().BeNull();
    }
}
