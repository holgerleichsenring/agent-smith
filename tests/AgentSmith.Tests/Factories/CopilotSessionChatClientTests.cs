using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders;
using AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders.Copilot;
using FluentAssertions;
using GitHub.Copilot;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Factories;

/// <summary>
/// 2026-09-07-d5f2: the Copilot adapter is driven through SCRIPTED session events.
/// The other providers are pinned through an injected HttpMessageHandler; the Copilot SDK talks to
/// a bundled CLI process rather than to HTTPS, so the seam the adapter owns is what the tests
/// stand in for. Nothing here needs a runtime binary or a Copilot seat.
/// </summary>
public sealed class CopilotSessionChatClientTests
{
    private static readonly CopilotSessionRequest Template =
        new(Model: "gpt-5", ReasoningEffort: null, SystemMessage: null, SeatToken: "seat", Tools: []);

    private static CopilotSessionChatClient NewClient(FakeCopilotRuntime runtime) =>
        new(runtime, Template, NullLogger<CopilotSessionChatClient>.Instance);

    [Fact]
    public async Task AssistantMessageThenIdle_ReturnsTheContent()
    {
        var runtime = new FakeCopilotRuntime();
        runtime.Script(new CopilotSessionEvent.AssistantMessage("the answer"), new CopilotSessionEvent.Idle());

        var response = await NewClient(runtime).GetResponseAsync([new ChatMessage(ChatRole.User, "the question")]);

        response.Text.Should().Be("the answer");
        runtime.Sessions.Should().HaveCount(1);
        runtime.Sessions[0].Prompts.Should().ContainSingle().Which.Should().Be("the question");
    }

    [Fact]
    public async Task StreamingRequested_YieldsDeltasThenTheFinalMessage()
    {
        var runtime = new FakeCopilotRuntime();
        runtime.Script(
            new CopilotSessionEvent.AssistantDelta("the "),
            new CopilotSessionEvent.AssistantDelta("answer"),
            new CopilotSessionEvent.AssistantMessage("the answer"),
            new CopilotSessionEvent.Idle());

        var updates = new List<string>();
        await foreach (var update in NewClient(runtime).GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "q")]))
            updates.Add(update.Text);

        updates.Should().StartWith(["the ", "answer"]);
    }

    [Fact]
    public async Task SessionError_FaultsTheCall()
    {
        var runtime = new FakeCopilotRuntime();
        runtime.Script(new CopilotSessionEvent.Failed("model unavailable"));

        var act = () => NewClient(runtime).GetResponseAsync([new ChatMessage(ChatRole.User, "q")]);

        (await act.Should().ThrowAsync<InvalidOperationException>()).WithMessage("*model unavailable*");
    }

    [Fact]
    public async Task SecondCallExtendingTheSentPrefix_SendsOnlyTheTail()
    {
        var runtime = new FakeCopilotRuntime();
        runtime.Script(new CopilotSessionEvent.AssistantMessage("first"), new CopilotSessionEvent.Idle());
        var client = NewClient(runtime);

        var history = new List<ChatMessage> { new(ChatRole.User, "one") };
        await client.GetResponseAsync(history);

        runtime.Script(new CopilotSessionEvent.AssistantMessage("second"), new CopilotSessionEvent.Idle());
        history.Add(new ChatMessage(ChatRole.Assistant, "first"));
        history.Add(new ChatMessage(ChatRole.User, "two"));
        await client.GetResponseAsync(history);

        // One session for both calls, and the second prompt carries only what the first did not.
        runtime.Sessions.Should().HaveCount(1);
        runtime.Sessions[0].Prompts.Should().HaveCount(2);
        runtime.Sessions[0].Prompts[1].Should().Contain("two").And.NotContain("one");
    }

    [Fact]
    public async Task PriorMessageEditedAtUnchangedLength_RebuildsAndDeletesTheOldSession()
    {
        var runtime = new FakeCopilotRuntime();
        runtime.Script(new CopilotSessionEvent.AssistantMessage("first"), new CopilotSessionEvent.Idle());
        var client = NewClient(runtime);

        await client.GetResponseAsync([
            new ChatMessage(ChatRole.User, "use this credential: hunter2"),
            new ChatMessage(ChatRole.Assistant, "ok"),
        ]);

        // The sensitive-tool scrub rewrites a message IN PLACE — the count is identical, which is
        // precisely why the watermark is a hash and not a length.
        runtime.Script(new CopilotSessionEvent.AssistantMessage("second"), new CopilotSessionEvent.Idle());
        await client.GetResponseAsync([
            new ChatMessage(ChatRole.User, "[set, applied earlier turn]"),
            new ChatMessage(ChatRole.Assistant, "ok"),
            new ChatMessage(ChatRole.User, "carry on"),
        ]);

        runtime.Sessions.Should().HaveCount(2, "a diverged history cannot continue in the old session");
        runtime.DeletedSessions.Should().ContainSingle().Which.Should().Be(runtime.Sessions[0].SessionId);
        runtime.Sessions[1].Prompts[0].Should().NotContain("hunter2");
    }

    [Fact]
    public async Task TurnCompletes_UsageDetailsCarryTheAccumulatorDelta()
    {
        var runtime = new FakeCopilotRuntime();
        // Session-wide accumulators: the session had already spent 100/10 before this call.
        runtime.UsageBefore = new CopilotUsage(100, 10, 5, 0.5, 1000);
        runtime.UsageAfter = new CopilotUsage(180, 35, 9, 1.25, 2400);
        runtime.Script(new CopilotSessionEvent.AssistantMessage("done"), new CopilotSessionEvent.Idle());

        var response = await NewClient(runtime).GetResponseAsync([new ChatMessage(ChatRole.User, "q")]);

        response.Usage!.InputTokenCount.Should().Be(80);
        response.Usage.OutputTokenCount.Should().Be(25);
        response.Usage.CachedInputTokenCount.Should().Be(4);
    }

    [Fact]
    public void RuntimeSessionConfig_UsesEmptyAllowlistAndNoSelfManagedContext()
    {
        var config = CopilotSessionFactory.BuildSessionConfig(Template);

        config.AvailableTools.Should().BeEmpty("an empty allowlist means the model reaches no tool at all");
        config.Tools.Should().BeEmpty();
        config.InfiniteSessions!.Enabled.Should().BeFalse("compaction belongs to CompactingChatClient");
        config.ToolSearch!.Enabled.Should().BeFalse();
        config.Streaming.Should().BeTrue();
    }

    [Fact]
    public void RuntimeSessionConfig_CarriesTheAgentsSeatTokenAndReplacesTheSystemPrompt()
    {
        var config = CopilotSessionFactory.BuildSessionConfig(Template with { SystemMessage = "you are the analyzer" });

        config.GitHubToken.Should().Be("seat", "a seat belongs to a person, so it rides on the session");
        config.SystemMessage!.Mode.Should().Be(SystemMessageMode.Replace);
        config.SystemMessage.Content.Should().Be("you are the analyzer");
    }

    [Fact]
    public void RuntimeClientOptions_NeverUseTheLoggedInUserAndRunInEmptyMode()
    {
        var runtimeBinary = Path.Combine(Path.GetTempPath(), $"copilot-{Guid.NewGuid():N}");
        File.WriteAllText(runtimeBinary, "#!/bin/sh\n");
        try
        {
            var options = CopilotSessionFactory.BuildClientOptions(runtimeBinary, "/tmp/base");

            options.Mode.Should().Be(CopilotClientMode.Empty, "CopilotCli mode is documented as unsafe for servers");
            options.UseLoggedInUser.Should().BeFalse("a cluster must never answer as whoever logged in on the build host");
            options.BaseDirectory.Should().Be("/tmp/base");
            options.Connection.Should().NotBeNull("a configured runtime path is how an air-gapped host finds the binary");
        }
        finally
        {
            File.Delete(runtimeBinary);
        }
    }

    [Fact]
    public void RuntimeClientOptions_ConfiguredRuntimeMissing_RefusesByName()
    {
        // The image was built without the Copilot runtime, or the mount is missing. Falling back
        // to a "bundled" runtime would be a lie: this repository's build never downloads one.
        var act = () => CopilotSessionFactory.BuildClientOptions("/no/such/copilot", "/tmp/base");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*COPILOT_CLI_PATH*").And.Message.Should().Contain("/no/such/copilot");
    }

    [Fact]
    public void Runtime_Resolved_DoesNotStartTheProcess()
    {
        // ConfigCapabilitiesTests resolves every registered builder from a real ServiceProvider and
        // CI has no runtime binary, so construction must not reach for one.
        var runtime = new CopilotRuntime(NullLoggerFactory.Instance);

        runtime.IsStarted.Should().BeFalse();
    }

    [Fact]
    public void Builder_ApiKeySecretConfigured_PrefersItOverTheEnvironment()
    {
        using var seat = new ScopedEnvironment(("AGENTSMITH_TEST_SEAT_A", "team-a-seat"),
            (AgentSmith.Contracts.Constants.AgentEnvKeys.CopilotGitHubToken, "fallback-seat"));
        var builder = new CopilotChatClientBuilder(new FakeCopilotRuntime(), NullLoggerFactory.Instance);
        var agent = new AgentConfig { Type = "copilot", Model = "gpt-5", ApiKeySecret = "AGENTSMITH_TEST_SEAT_A" };

        var client = builder.Build(agent, new ModelAssignment { Model = "gpt-5" });

        client.Should().BeOfType<CopilotSessionChatClient>();
    }

    [Fact]
    public void Builder_NoTokenAnywhere_RefusesWithApiKeySecretAndTheEnvVarNames()
    {
        using var cleared = new ScopedEnvironment(
            (AgentSmith.Contracts.Constants.AgentEnvKeys.CopilotGitHubToken, null),
            ("GH_TOKEN", null),
            (AgentSmith.Contracts.Constants.AgentEnvKeys.GitHubToken, null));
        var builder = new CopilotChatClientBuilder(new FakeCopilotRuntime(), NullLoggerFactory.Instance);

        var act = () => builder.Build(new AgentConfig { Type = "copilot", Model = "gpt-5" }, new ModelAssignment());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*api_key_secret*").And.Message.Should().Contain("COPILOT_GITHUB_TOKEN");
    }

    [Fact]
    public void Builder_SupportedTypes_IsTheSingleNameCopilot()
    {
        // ConfigStudioCapabilities hands the dashboard these verbatim; a second spelling would
        // appear as a second provider for one provider.
        new CopilotChatClientBuilder(new FakeCopilotRuntime(), NullLoggerFactory.Instance)
            .SupportedTypes.Should().Equal("copilot");
    }

    /// <summary>Restores the environment it changed, so the tests stay order-independent.</summary>
    private sealed class ScopedEnvironment : IDisposable
    {
        private readonly List<(string Name, string? Previous)> _previous = [];

        public ScopedEnvironment(params (string Name, string? Value)[] values)
        {
            foreach (var (name, value) in values)
            {
                _previous.Add((name, Environment.GetEnvironmentVariable(name)));
                Environment.SetEnvironmentVariable(name, value);
            }
        }

        public void Dispose()
        {
            foreach (var (name, previous) in _previous) Environment.SetEnvironmentVariable(name, previous);
        }
    }
}
