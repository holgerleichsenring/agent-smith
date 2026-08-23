using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Core.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Configuration;

/// <summary>
/// p0506: the shipped example writes <c>secret: ${JIRA_WEBHOOK_SECRET}</c> and the docs
/// promise the value lives in the environment. Nothing expanded it, so an operator who
/// copied the example held the literal placeholder as a "configured" secret — a value no
/// Jira delivery can ever match. The reference now resolves, and one that resolves to
/// nothing reads as UNCONFIGURED rather than becoming an unmatchable secret.
/// </summary>
public sealed class JiraTriggerSecretReferenceTests
{
    [Fact]
    public void Materialize_JiraTriggerSecretReference_ResolvesFromTheEnvironment()
    {
        var config = Materialize(Yaml("${JIRA_WEBHOOK_SECRET}"), env: "from-the-environment");

        Secret(config).Should().Be("from-the-environment");
    }

    [Fact]
    public void Materialize_JiraTriggerSecretReference_ResolvesFromTheSecretsMap()
    {
        var yaml = Yaml("${jira_webhook_secret}") + """

            secrets:
              jira_webhook_secret: from-the-secrets-map
            """;

        var config = Materialize(yaml, env: null);

        Secret(config).Should().Be("from-the-secrets-map");
    }

    [Fact]
    public void Materialize_JiraTriggerSecretReferenceThatResolvesToNothing_IsUnconfigured()
    {
        var config = Materialize(Yaml("${JIRA_WEBHOOK_SECRET}"), env: null);

        Secret(config).Should().BeEmpty(
            "an unresolved reference is an absent secret, not a secret nobody can match");
    }

    [Fact]
    public void Materialize_JiraTriggerSecretWrittenLiterally_IsLeftAlone()
    {
        var config = Materialize(Yaml("written-out-in-full"), env: "from-the-environment");

        Secret(config).Should().Be("written-out-in-full");
    }

    private static string Secret(AgentSmithConfig config) =>
        config.Projects["demo"].JiraTrigger!.Secret!;

    private static string Yaml(string secret) => $$"""
        agents:
          a: { type: Claude }
        repos:
          r: { type: GitHub, url: https://x, auth: t }
        trackers:
          t: { type: Jira, auth: t }
        projects:
          demo:
            agent: a
            tracker: t
            repos: [r]
            jira_trigger:
              secret: {{secret}}
              project_resolution: { strategy: tag, value: demo }
        """;

    private static AgentSmithConfig Materialize(string yaml, string? env) =>
        new RawConfigMaterializer(
                new ProjectConfigNormalizer(),
                new EffectiveTriggerBuilder(),
                new DeploymentDefaultsApplier(),
                new ConfigCatalogResolver(),
                new AgentSmithPaths(),
                secretReferences: new ConfigSecretReferences(_ => env))
            .Materialize(new RawConfigYaml().Deserialize(yaml));
}
