using AgentSmith.Infrastructure.Services.Sandbox;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.Architecture;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-10-25-c5a7h: a real in-process step running env prints no declared or known secret,
/// and a git token the step brings in its own Env still arrives. It sets process environment
/// variables; the external-process collection runs alone, so nothing else sees them.
/// </summary>
[Trait("TestProcess", "serial")]
[Collection(ExternalProcessCollection.Name)]
public sealed class InProcessSandboxSecretEnvTests : IDisposable
{
    private static readonly string[] Variables = ["REDIS_URL", "ANTHROPIC_API_KEY", "C5A7H_STAGED_FEED"];
    private readonly Dictionary<string, string?> _previous = Variables.ToDictionary(n => n, Environment.GetEnvironmentVariable);
    private readonly string _dir = Directory.CreateTempSubdirectory("c5a7h-").FullName;

    [Fact]
    public async Task InProcessSandbox_RunEnv_PrintsNoSecret()
    {
        Environment.SetEnvironmentVariable("REDIS_URL", "redis:6379,password=redis-secret-value");
        Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", "sk-ant-model-key-value");
        Environment.SetEnvironmentVariable("C5A7H_STAGED_FEED", "known-registry-token");
        await using var sandbox = new InProcessSandbox("c5a7h", _dir, ownsWorkDir: false,
            InProcessStepEnvironment.For(null, ["known-registry-token"]), NullLogger.Instance);

        var result = await sandbox.RunStepAsync(new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "/usr/bin/env", Env: new Dictionary<string, string> { ["GIT_TOKEN"] = "step-git-token" },
            TimeoutSeconds: 60), progress: null, CancellationToken.None);

        result.OutputContent.Should().NotContain("redis-secret-value").And.NotContain("sk-ant-model-key-value")
            .And.NotContain("known-registry-token").And.Contain("GIT_TOKEN=step-git-token");
    }

    public void Dispose()
    {
        foreach (var (name, value) in _previous) Environment.SetEnvironmentVariable(name, value);
        Directory.Delete(_dir, recursive: true);
    }
}
