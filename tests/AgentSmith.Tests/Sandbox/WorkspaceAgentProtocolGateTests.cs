using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Server.Services.Sandbox;
using FluentAssertions;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-11-25-9992d: a workspace whose project scopes secrets to repositories refuses an agent below protocol 3, which
/// would read the step and silently drop its scope; one without scoped secrets asks nothing.
/// </summary>
public sealed class WorkspaceAgentProtocolGateTests
{
    private static readonly ResolvedSandboxSecrets Scoped = new(
        [], [], [new SecretEnvBinding("DB_PASSWORD", new SecretRef("api-creds", "db"), "api")]);

    private static SandboxSpec Spec(ResolvedSandboxSecrets? secrets) =>
        new("ignored", ResourceLimits.Default, AgentImage: "registry.example/agent-smith-sandbox-agent:0.200.0",
            Secrets: secrets);

    [Fact]
    public async Task RefuseOldAgentsAsync_AnAgentOnProtocol2_FailsNamingImageAndProtocol()
    {
        var old = new AnsweringContainer(2);

        var act = () => new WorkspaceAgentProtocolGate().RefuseOldAgentsAsync(
            Spec(Scoped), [("dotnet:9", new AnsweringContainer(3)), ("node:20", old)], CancellationToken.None);

        (await act.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should()
            .Contain("registry.example/agent-smith-sandbox-agent:0.200.0")
            .And.Contain("node:20")
            .And.Contain("wire protocol 2")
            .And.Contain("sandbox.secrets.repo_env needs protocol 3");
        old.Steps.Should().ContainSingle().Which.Kind.Should().Be(StepKind.ListFiles);
    }

    [Fact]
    public async Task RefuseOldAgentsAsync_AgentsOnProtocol3_Pass()
    {
        var current = new AnsweringContainer(3);

        await new WorkspaceAgentProtocolGate().RefuseOldAgentsAsync(
            Spec(Scoped), [("dotnet:9", current)], CancellationToken.None);

        current.Steps.Should().ContainSingle("each container is asked once");
    }

    [Fact]
    public async Task RefuseOldAgentsAsync_NoScopedSecrets_AsksNothing()
    {
        var old = new AnsweringContainer(2);
        var projectWide = new ResolvedSandboxSecrets([new SecretEnvBinding("FEED", new SecretRef("b", "f"))], []);

        await new WorkspaceAgentProtocolGate().RefuseOldAgentsAsync(Spec(projectWide), [("dotnet:9", old)], CancellationToken.None);
        await new WorkspaceAgentProtocolGate().RefuseOldAgentsAsync(Spec(null), [("dotnet:9", old)], CancellationToken.None);

        old.Steps.Should().BeEmpty("a project that scopes nothing keeps today's behaviour, older agents included");
    }

    private sealed class AnsweringContainer(int protocol) : ISandbox
    {
        public List<Step> Steps { get; } = [];

        public string JobId => "answering";

        public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? progress, CancellationToken cancellationToken)
        {
            Steps.Add(step);
            return Task.FromResult(new StepResult(protocol, step.StepId, 0, false, 0.01, null, "/"));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
