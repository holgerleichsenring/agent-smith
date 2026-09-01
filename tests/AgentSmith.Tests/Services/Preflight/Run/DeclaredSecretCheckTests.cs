using AgentSmith.Application.Services.Preflight.Run;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Preflight;
using FluentAssertions;

namespace AgentSmith.Tests.Services.Preflight.Run;

/// <summary>
/// 2026-08-28-b630: what is wrong with a declared credential is decidable from the
/// declaration, so it is decided before the run spends anything — and without a cluster
/// call, which the deployment's role could not make and must never be granted.
/// </summary>
public sealed class DeclaredSecretCheckTests
{
    [Fact]
    public async Task Preflight_AMalformedSecretReference_IsNamedBeforeAnyRun()
    {
        var finding = await Check(new SandboxSecrets
        {
            Env = new() { ["SF_CLIENT_ID"] = "no-colon-here", ["SF_USER"] = "sf-creds:user" }
        });

        finding.Verdict.Should().Be(RunPreflightVerdict.Fail);
        finding.Message.Should().Contain("SF_CLIENT_ID").And.NotContain("SF_USER");
        finding.Message.Should().NotContain("no-colon-here",
            "a field an operator mistyped may hold the value they meant to reference");
        finding.Lever.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Preflight_TwoFilesClaimingOneMount_IsRefused()
    {
        var finding = await Check(new SandboxSecrets
        {
            Files =
            [
                new SandboxSecretFile { Mount = "/secrets/key", Secret = "creds", Key = "a" },
                new SandboxSecretFile { Mount = "/secrets/key", Secret = "creds", Key = "b" }
            ]
        });

        finding.Verdict.Should().Be(RunPreflightVerdict.Fail);
        finding.Message.Should().Contain("/secrets/key").And.Contain("twice");
    }

    [Fact]
    public async Task Preflight_EveryReferenceWellFormed_Passes()
    {
        var finding = await Check(new SandboxSecrets
        {
            Env = new() { ["SF_CLIENT_ID"] = "sf-creds:client-id" },
            Files = [new SandboxSecretFile { Mount = "/secrets/key", Secret = "sf-creds", Key = "jwt" }]
        });

        finding.Verdict.Should().Be(RunPreflightVerdict.Pass);
        finding.Message.Should().Contain("2");
    }

    [Fact]
    public async Task Preflight_NothingDeclared_HasNothingToSay()
    {
        var finding = await Check(secrets: null);

        finding.Verdict.Should().Be(RunPreflightVerdict.Pass);
    }

    private static async Task<RunPreflightFinding> Check(SandboxSecrets? secrets)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ProjectConfig,
            new ResolvedProject { Sandbox = new SandboxConfig { Secrets = secrets } });
        return await new DeclaredSecretCheck().RunAsync(pipeline, CancellationToken.None);
    }
}
