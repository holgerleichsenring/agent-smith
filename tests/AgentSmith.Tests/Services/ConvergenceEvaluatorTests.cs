using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Tests.TestHelpers;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services;

public sealed class ConvergenceEvaluatorTests
{
    [Fact]
    public async Task EvaluateAsync_ConsensusJson_ReturnsConsensusResult()
    {
        const string responseJson = """
            {"consensus": true, "observation_ids": [1, 2], "links": [], "additional_roles": []}
            """;
        var sut = Build(responseJson);
        var observations = SampleObservations();

        var result = await sut.EvaluateAsync(
            new AgentConfig { Type = "test" }, observations, _ => { }, CancellationToken.None);

        result.Consensus.Should().BeTrue();
        result.Observations.Should().BeEquivalentTo(observations);
    }

    [Fact]
    public async Task EvaluateAsync_ParseFailure_ReturnsNonConsensusOverInputs()
    {
        var sut = Build("not parseable at all");
        var observations = SampleObservations();

        var result = await sut.EvaluateAsync(
            new AgentConfig { Type = "test" }, observations, _ => { }, CancellationToken.None);

        result.Consensus.Should().BeFalse();
        result.Observations.Should().BeEquivalentTo(observations);
        result.Blocking.Should().HaveCount(1, "the seeded blocking observation must surface");
        result.NonBlocking.Should().HaveCount(1);
    }

    [Fact]
    public async Task EvaluateAsync_CostSink_InvokedOncePerCall()
    {
        var sut = Build("""{"consensus": true}""");
        var costCalls = 0;

        await sut.EvaluateAsync(
            new AgentConfig { Type = "test" }, SampleObservations(),
            _ => costCalls++, CancellationToken.None);

        costCalls.Should().Be(1);
    }

    [Fact]
    public async Task EvaluateAsync_FencedJson_StillParsed()
    {
        const string responseJson = """
            ```json
            {"consensus": true, "observation_ids": [1, 2], "links": [], "additional_roles": []}
            ```
            """;
        var sut = Build(responseJson);

        var result = await sut.EvaluateAsync(
            new AgentConfig { Type = "test" }, SampleObservations(), _ => { }, CancellationToken.None);

        result.Consensus.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateAsync_ParseFailure_NonBlockingSplitMatchesInputs()
    {
        var sut = Build("nope");
        var observations = SampleObservations();

        var result = await sut.EvaluateAsync(
            new AgentConfig { Type = "test" }, observations, _ => { }, CancellationToken.None);

        (result.Blocking.Count + result.NonBlocking.Count).Should().Be(observations.Count);
    }

    private static ConvergenceEvaluator Build(string responseText)
    {
        var stub = new StubChatClient(new Queue<string>(new[] { responseText }));
        var factory = new StubChatClientFactory(stub);
        var prompts = new FakePromptCatalog().WithPrompt("convergence-system", "system");
        var parser = TolerantJsonParserFactory.CreateConvergence();
        return new ConvergenceEvaluator(factory, prompts, parser,
            NullLogger<ConvergenceEvaluator>.Instance);
    }

    private static IReadOnlyList<SkillObservation> SampleObservations() => new[]
    {
        new SkillObservation(
            Id: 1, Role: "alpha", Concern: ObservationConcern.Security,
            Description: "blocker", Suggestion: "fix",
            Blocking: true, Severity: ObservationSeverity.High, Confidence: 80),
        new SkillObservation(
            Id: 2, Role: "beta", Concern: ObservationConcern.Correctness,
            Description: "nit", Suggestion: "noop",
            Blocking: false, Severity: ObservationSeverity.Low, Confidence: 50),
    };
}
