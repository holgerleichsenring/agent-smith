using AgentSmith.Application.Services.Builders;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// p0272: the builder parses the operator's sandbox.secrets block onto the spec
/// (fail-fast on a malformed 'secretName:key' reference — operator config is not
/// guessed around).
/// </summary>
public sealed class SandboxSpecBuilderSecretsTests
{
    private static SandboxSpecBuilder NewSut() =>
        new(new StubSandboxResourceResolver(), new StubAgentImageResolver());

    [Fact]
    public void Build_SandboxSecrets_MapOntoSpecBindings()
    {
        var project = new ResolvedProject
        {
            Sandbox = new SandboxConfig
            {
                Secrets = new SandboxSecrets
                {
                    Env = new() { ["SF_CLIENT_ID"] = "sf-creds:client-id" },
                    Files =
                    [
                        new SandboxSecretFile { Mount = "/secrets/server.key", Secret = "sf-creds", Key = "jwt-key" }
                    ]
                }
            }
        };

        var spec = NewSut().Build(project, language: "node", pipelineName: "fix-bug");

        var env = spec.Secrets!.Env.Single();
        env.EnvName.Should().Be("SF_CLIENT_ID");
        env.Source.SecretName.Should().Be("sf-creds");
        env.Source.Key.Should().Be("client-id");
        var file = spec.Secrets.Files.Single();
        file.MountPath.Should().Be("/secrets/server.key");
        file.Source.SecretName.Should().Be("sf-creds");
        file.Source.Key.Should().Be("jwt-key");
    }

    [Fact]
    public void Build_MalformedSecretRef_ThrowsAtConfigTime()
    {
        var project = new ResolvedProject
        {
            Sandbox = new SandboxConfig
            {
                Secrets = new SandboxSecrets { Env = new() { ["SF_CLIENT_ID"] = "no-colon-here" } }
            }
        };

        var act = () => NewSut().Build(project, language: "node", pipelineName: "fix-bug");

        act.Should().Throw<ArgumentException>().WithMessage("*SF_CLIENT_ID*");
    }
}
