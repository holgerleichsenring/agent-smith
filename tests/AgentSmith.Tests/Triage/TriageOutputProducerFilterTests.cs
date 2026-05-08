using AgentSmith.Application.Services.Activation;
using AgentSmith.Application.Services.Triage;
using AgentSmith.Contracts.Activation;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Triage;

public sealed class TriageOutputProducerFilterTests
{
    [Fact]
    public async Task ProduceAsync_FilterNarrowsBeforeLlm_LlmSeesOnlyMatchingSkills()
    {
        var stub = NewCapturingChat("""{"phases":{"plan":{"lead":"matches","analysts":[],"reviewers":[],"filter":null}},"confidence":80,"rationale":"r"}""");
        var producer = NewProducer(stub);
        var pipeline = NewPipelineWithSkills(
            new RoleSkillDefinition
            {
                Name = "matches",
                Description = "matches",
                RolesSupported = [SkillRole.Lead],
                ActivatesWhen = "source_available",
            },
            new RoleSkillDefinition
            {
                Name = "filtered_out",
                Description = "filtered",
                RolesSupported = [SkillRole.Lead],
                ActivatesWhen = "NOT source_available",
            });
        SetSourceAvailable(pipeline, value: true);

        await producer.ProduceAsync(pipeline, CancellationToken.None);

        var prompt = stub.LastUserMessageText;
        prompt.Should().Contain("matches");
        prompt.Should().NotContain("filtered_out");
    }

    [Fact]
    public async Task ProduceAsync_NoActivatesWhenSkills_AllPassToLlm()
    {
        var stub = NewCapturingChat("""{"phases":{"plan":{"lead":null,"analysts":[],"reviewers":[],"filter":null}},"confidence":80,"rationale":"r"}""");
        var producer = NewProducer(stub);
        var pipeline = NewPipelineWithSkills(
            new RoleSkillDefinition
            {
                Name = "legacy_a",
                Description = "x",
                RolesSupported = [SkillRole.Lead],
                ActivatesWhen = null,
            },
            new RoleSkillDefinition
            {
                Name = "legacy_b",
                Description = "y",
                RolesSupported = [SkillRole.Analyst],
                ActivatesWhen = null,
            });

        await producer.ProduceAsync(pipeline, CancellationToken.None);

        stub.LastUserMessageText.Should().Contain("legacy_a").And.Contain("legacy_b");
    }

    [Fact]
    public async Task ProduceAsync_PhaseExceedsCap_TrimmedBySpecificity()
    {
        var stub = NewCapturingChat("""{"phases":{"plan":{"lead":null,"analysts":["high","mid","low"],"reviewers":[],"filter":null}},"confidence":80,"rationale":"r"}""");
        var producer = NewProducer(stub);
        var pipeline = NewPipelineWithSkills(
            new RoleSkillDefinition
            {
                Name = "high",
                Description = "h",
                RolesSupported = [SkillRole.Analyst],
                ActivatesWhen = "source_available AND context_yaml_present",
            },
            new RoleSkillDefinition
            {
                Name = "mid",
                Description = "m",
                RolesSupported = [SkillRole.Analyst],
                ActivatesWhen = "source_available",
            },
            new RoleSkillDefinition
            {
                Name = "low",
                Description = "l",
                RolesSupported = [SkillRole.Analyst],
                ActivatesWhen = null,
            });
        SetSourceAvailable(pipeline, value: true);
        SetContextYamlPresent(pipeline, value: true);

        var output = await producer.ProduceAsync(pipeline, CancellationToken.None);

        output.Phases[PipelinePhase.Plan].Analysts.Should().BeEquivalentTo(["high", "mid"]);
    }

    private static TriageOutputProducer NewProducer(CapturingChatClient stub)
    {
        var parser = new ActivationExpressionParser(new ActivationExpressionTokenizer());
        return new TriageOutputProducer(
            new ProjectMapExcerptBuilder(),
            new TriageOutputValidator(new TriageRationaleParser()),
            new TriageLabelOverrideApplier(),
            new TestPromptCatalog(),
            new CapturingChatClientFactory(stub),
            new ActivationSkillFilter(parser, new ActivationEvaluator(),
                NullLogger<ActivationSkillFilter>.Instance),
            new PhaseSpecificityTrimmer(
                new ActivationSpecificityScorer(parser, NullLogger<ActivationSpecificityScorer>.Instance),
                NullLogger<PhaseSpecificityTrimmer>.Instance),
            RunStateConceptsTestFactory.Default,
            new LoopLimitsConfig { MaxSkillsPerPhase = 2 },
            NullLogger<TriageOutputProducer>.Instance);
    }

    private static CapturingChatClient NewCapturingChat(string responseJson) => new(responseJson);

    private static PipelineContext NewPipelineWithSkills(params RoleSkillDefinition[] skills)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.AvailableRoles, (IReadOnlyList<RoleSkillDefinition>)skills);
        pipeline.Set(ContextKeys.AgentConfig, new AgentConfig());
        return pipeline;
    }

    private static void SetSourceAvailable(PipelineContext pipeline, bool value) =>
        RunStateConceptsTestFactory.Default(pipeline).SetBool("source_available", value);

    private static void SetContextYamlPresent(PipelineContext pipeline, bool value) =>
        RunStateConceptsTestFactory.Default(pipeline).SetBool("context_yaml_present", value);
}

internal sealed class CapturingChatClient(string responseJson) : IChatClient
{
    public string LastUserMessageText { get; private set; } = string.Empty;

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var user = messages.LastOrDefault(m => m.Role == ChatRole.User);
        LastUserMessageText = user?.Text ?? string.Empty;
        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, responseJson)));
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;
    public void Dispose() { }
}

internal sealed class CapturingChatClientFactory(CapturingChatClient client) : IChatClientFactory
{
    public IChatClient Create(AgentConfig agent, TaskType task, int? maxIterations = null) => client;
    public int GetMaxOutputTokens(AgentConfig agent, TaskType task) => 8192;
    public string GetModel(AgentConfig agent, TaskType task) => "stub";
}

internal sealed class TestPromptCatalog : IPromptCatalog
{
    public string Get(string name) => "system";

    public string Render(string name, IReadOnlyDictionary<string, string> tokens)
    {
        return string.Join("\n", tokens.Select(t => $"{t.Key}: {t.Value}"));
    }
}
