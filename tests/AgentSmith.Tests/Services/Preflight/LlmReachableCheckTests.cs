using AgentSmith.Application.Services.Preflight.Checks;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Preflight;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Services.Preflight;

/// <summary>
/// p0324: llm-reachable probes every configured agent through the factory seam and
/// turns an auth failure into an actionable fix hint (key secret, Azure deployment,
/// subscription rate-limit tier) — never a stack trace.
/// </summary>
public sealed class LlmReachableCheckTests
{
    [Fact]
    public async Task LlmReachableCheck_InvalidKey_FailsActionable()
    {
        var config = ConfigWithAgent("claude", new AgentConfig { Type = "claude", Model = "claude-sonnet-4-6" });
        var factory = new ScriptedChatClientFactory(
            ConnectionProbeResult.Unreachable(210, "401 Unauthorized: invalid x-api-key"));
        var check = new LlmReachableCheck(FakePreflightConfigSource.Of(config), factory);

        var result = await check.RunAsync(CancellationToken.None);

        result.Status.Should().Be(PreflightStatus.Fail);
        result.Message.Should().Contain("claude").And.Contain("401 Unauthorized");
        result.FixHint.Should().Contain("api_key_secret").And.Contain("rate_limit");
    }

    [Fact]
    public async Task RunAsync_NoAgents_Skips()
    {
        var check = new LlmReachableCheck(
            FakePreflightConfigSource.Of(new AgentSmithConfig()),
            new ScriptedChatClientFactory(ConnectionProbeResult.Reachable(1)));

        var result = await check.RunAsync(CancellationToken.None);

        result.Status.Should().Be(PreflightStatus.Skip);
    }

    [Fact]
    public async Task RunAsync_Reachable_PassesAndSurfacesRateLimitConfiguration()
    {
        var config = ConfigWithAgent("gpt", new AgentConfig { Type = "openai", Model = "gpt-4.1" });
        var check = new LlmReachableCheck(
            FakePreflightConfigSource.Of(config),
            new ScriptedChatClientFactory(ConnectionProbeResult.Reachable(320)));

        var result = await check.RunAsync(CancellationToken.None);

        result.Status.Should().Be(PreflightStatus.Pass);
        result.Message.Should().Contain("gpt").And.Contain("rate_limit not set");
    }

    private static AgentSmithConfig ConfigWithAgent(string name, AgentConfig agent) => new()
    {
        Agents = new Dictionary<string, AgentConfig> { [name] = agent },
    };

    /// <summary>Scripted factory — avoids mocking the default interface method.</summary>
    private sealed class ScriptedChatClientFactory(ConnectionProbeResult probeResult) : IChatClientFactory
    {
        public Task<ConnectionProbeResult> ProbeAsync(AgentConfig agent, CancellationToken cancellationToken) =>
            Task.FromResult(probeResult);

        public IChatClient Create(AgentConfig agent, TaskType task, int? maxIterations = null, AgentSmith.Contracts.Providers.MasterLoopHooks? masterLoopHooks = null) =>
            throw new NotSupportedException("preflight probes never create a client");

        public int GetMaxOutputTokens(AgentConfig agent, TaskType task) => 0;

        public string GetModel(AgentConfig agent, TaskType task) => agent.Model;
    }
}
