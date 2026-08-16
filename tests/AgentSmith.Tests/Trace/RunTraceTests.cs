using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using FluentAssertions;

namespace AgentSmith.Tests.Trace;

/// <summary>
/// p0423: the trace is the instrument that turns "run it again and watch" into "read what
/// happened". It is worth nothing if it cannot be switched on where the failure happens,
/// and worse than nothing if it carries a credential.
/// </summary>
public sealed class RunTraceTests
{
    [Fact]
    public void StagedCredential_NeverAppearsInATrace()
    {
        var config = new AgentSmithConfig
        {
            Registries = [new RegistryConfig("packages.example.test", "ci", "glpat-SECRETVALUE123")],
            Secrets = new Dictionary<string, string> { ["AZDO_PAT"] = "pat-9f2c11ab4400" },
        };
        var masker = new SecretMasker(config);

        var masked = masker.Apply(
            "//packages.example.test/:_authToken=glpat-SECRETVALUE123\nAuthorization: Bearer pat-9f2c11ab4400");

        masked.Should().NotContain("glpat-SECRETVALUE123").And.NotContain("pat-9f2c11ab4400");
        masked.Should().Contain("packages.example.test", "the host is not the secret — the token is");
    }

    /// <summary>
    /// A token that CONTAINS a shorter secret must not be left half-masked by the shorter
    /// replacement running first — the residue would still identify the real value.
    /// </summary>
    [Fact]
    public void ALongerSecret_IsMaskedBeforeAShorterOneItContains()
    {
        var config = new AgentSmithConfig
        {
            Secrets = new Dictionary<string, string>
            {
                ["SHORT"] = "abc123",
                ["LONG"] = "abc123-and-then-some-more",
            },
        };

        new SecretMasker(config).Apply("token=abc123-and-then-some-more")
            .Should().Be("token=***");
    }

    [Fact]
    public void ValuesTooShortToBeSecret_AreLeftAlone()
    {
        var config = new AgentSmithConfig
        {
            Secrets = new Dictionary<string, string> { ["EMPTY"] = "", ["TINY"] = "dev" },
        };

        new SecretMasker(config).Apply("dev branch, dev machine")
            .Should().Be("dev branch, dev machine",
                "masking three-letter values would redact the prose the trace exists for");
    }

    [Fact]
    public void TheSwitch_IsOffByDefault()
        => new TraceSwitch(new AgentSmithConfig()).IsOn.Should().BeFalse();

    [Fact]
    public void TheSwitch_ReadsTheConfiguration()
        => new TraceSwitch(new AgentSmithConfig { Trace = new TraceConfig { Enabled = true } })
            .IsOn.Should().BeTrue();

    /// <summary>
    /// The environment wins: a k8s Job and a compose service are configured by environment
    /// and mount their config read-only, so a switch that only lives in the file cannot be
    /// flipped where the failure is.
    /// </summary>
    [Theory]
    [InlineData("1", true)]
    [InlineData("true", true)]
    [InlineData("on", true)]
    [InlineData("0", false)]
    [InlineData("", false)]
    public void TheEnvironment_OverridesTheConfiguration(string value, bool expected)
    {
        var previous = Environment.GetEnvironmentVariable(TraceSwitch.EnvironmentVariable);
        Environment.SetEnvironmentVariable(TraceSwitch.EnvironmentVariable, value);
        try
        {
            new TraceSwitch(new AgentSmithConfig { Trace = new TraceConfig { Enabled = !expected } })
                .IsOn.Should().Be(expected);
        }
        finally { Environment.SetEnvironmentVariable(TraceSwitch.EnvironmentVariable, previous); }
    }

    [Fact]
    public async Task UntracedRun_WritesNoConversation()
    {
        var writer = new AgentSmith.Contracts.Runs.NullRunTraceWriter();

        writer.IsEnabled.Should().BeFalse(
            "producers ask before composing, so an untraced run pays nothing");
        await writer.WriteAsync("run", "prompt", "anything", CancellationToken.None);
    }
}
