using AgentSmith.Application.Services.Preflight.Run;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Preflight;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;

namespace AgentSmith.Tests.Services.Preflight.Run;

/// <summary>
/// 2026-08-28-b630: presence is proven where the value arrives. The pod started, so
/// Kubernetes already resolved the reference; what is left is that the variable is set and
/// the file is there — read by NAME, so nothing the finding prints can carry a value.
/// </summary>
public sealed class InjectedSecretCheckTests
{
    private static readonly SecretRef Source = new("sf-creds", "client-id");

    [Fact]
    public async Task Sandbox_AnUnsetInjectedVariable_FailsNamingTheVariableNotItsValue()
    {
        var injected = new ResolvedSandboxSecrets(
            [new SecretEnvBinding("SF_CLIENT_ID", Source), new SecretEnvBinding("SF_USER", Source)],
            [new SecretFileMount("/secrets/jwt.key", Source)]);
        var sandbox = new InjectingSandbox(injected, present: ["SF_USER", "/secrets/jwt.key"]);

        var finding = await Check(sandbox, declared: injected);

        finding.Verdict.Should().Be(RunPreflightVerdict.Fail);
        finding.Message.Should().Contain("SF_CLIENT_ID").And.NotContain("SF_USER");
        sandbox.Script.Should().Contain("[ -n \"${SF_CLIENT_ID:-}\" ]",
            "the test reads whether the variable is non-empty, never what is in it");
        sandbox.Script.Should().NotContain("echo \"$SF_CLIENT_ID\"");
    }

    [Fact]
    public async Task Sandbox_EveryInjectedCredentialPresent_Passes()
    {
        var injected = new ResolvedSandboxSecrets(
            [new SecretEnvBinding("SF_CLIENT_ID", Source)], [new SecretFileMount("/secrets/jwt.key", Source)]);
        var sandbox = new InjectingSandbox(injected, present: ["SF_CLIENT_ID", "/secrets/jwt.key"]);

        var finding = await Check(sandbox, declared: injected);

        finding.Verdict.Should().Be(RunPreflightVerdict.Pass);
    }

    [Fact]
    public async Task Sandbox_ABackendThatInjectsNothing_ReportsNotInjected()
    {
        var declared = new ResolvedSandboxSecrets([new SecretEnvBinding("SF_CLIENT_ID", Source)], []);

        var finding = await Check(new StubSandbox(), declared);

        finding.Verdict.Should().Be(RunPreflightVerdict.Warn,
            "a repository is not broken because the backend running it carries no secrets");
        finding.Message.Should().Contain("NOT INJECTED");
    }

    private static async Task<RunPreflightFinding> Check(ISandbox sandbox, ResolvedSandboxSecrets declared)
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes, new Dictionary<string, ISandbox> { ["default"] = sandbox });
        pipeline.Set(ContextKeys.ProjectConfig, Project(declared));
        return await new InjectedSecretCheck(new SandboxSecretPresenceProbe())
            .RunAsync(pipeline, CancellationToken.None);
    }

    private static ResolvedProject Project(ResolvedSandboxSecrets declared) =>
        new()
        {
            Sandbox = new SandboxConfig
            {
                Secrets = new SandboxSecrets
                {
                    Env = declared.Env.ToDictionary(
                        e => e.EnvName, e => $"{e.Source.SecretName}:{e.Source.Key}"),
                    Files = [.. declared.Files.Select(f => new SandboxSecretFile
                    {
                        Mount = f.MountPath, Secret = f.Source.SecretName, Key = f.Source.Key
                    })]
                }
            }
        };
}
