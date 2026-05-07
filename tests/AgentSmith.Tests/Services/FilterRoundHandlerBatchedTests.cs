using System.Text.Json;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services;

public sealed class FilterRoundHandlerBatchedTests
{
    [Fact]
    public void RenderForFilter_OmitsDetails()
    {
        var observations = new List<SkillObservation>
        {
            ObservationFactory.Make("HIGH", "src/a.cs", 1, "headline finding", "do this", 80) with
            {
                Details = "this is a long-form body that should never appear in filter input"
            }
        };

        var rendered = FilterRoundHandler.RenderForFilter(observations);

        rendered.Should().NotContain("long-form body");
        rendered.Should().Contain("headline finding");
    }

    [Fact]
    public async Task Filter_SmallList_OneBatch_ReducesCorrectly()
    {
        var input = MakeObservations(5);
        var keepResponse = JsonSerializer.Serialize(input.Take(3).Select(o => Materialize(o)));
        var responses = new Queue<string>(new[] { keepResponse });

        var sut = BuildHandler(responses);
        var pipeline = NewPipelineWithSkill();
        pipeline.Set<List<SkillObservation>>(ContextKeys.SkillObservations, input);

        var result = await sut.ExecuteAsync(NewContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var stored = pipeline.Get<List<SkillObservation>>(ContextKeys.SkillObservations);
        stored.Should().HaveCount(3, "filter kept 3 of 5");
    }

    [Fact]
    public async Task Filter_AllBatchesFail_AllOriginalsRetainedPlusCoverageObservation()
    {
        var input = MakeObservations(5);
        // Garbage response — neither strict parse nor resilient extraction yields anything.
        var responses = new Queue<string>(new[] { "I cannot filter this." });

        var sut = BuildHandler(responses);
        var pipeline = NewPipelineWithSkill();
        pipeline.Set<List<SkillObservation>>(ContextKeys.SkillObservations, input);

        var result = await sut.ExecuteAsync(NewContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var stored = pipeline.Get<List<SkillObservation>>(ContextKeys.SkillObservations);
        stored.Should().HaveCount(6, "5 originals retained + 1 coverage observation");
        stored.Last().Concern.Should().Be(ObservationConcern.Correctness);
        stored.Last().Severity.Should().Be(ObservationSeverity.Info);
        stored.Last().Category.Should().Be("meta");
        stored.Last().Description.Should().Contain("Filter coverage incomplete");
    }

    [Fact]
    public async Task Filter_FilteredOutput_PreservesDetails()
    {
        var input = new List<SkillObservation>
        {
            ObservationFactory.Make("HIGH", "src/a.cs", 1, "headline", "fix it", 80) with
            {
                Details = "long-form body content that should survive the filter round-trip"
            }
        };
        // LLM response: keep this observation. Note the response itself doesn't include Details
        // (filter prompt doesn't show Details) — the framework should preserve Details from the
        // original observation when the filter keeps it. We assert via filter returning the
        // observation by description, and the orchestration layer is expected to preserve.
        // For this test we just verify the input observation's Details survives one round.
        var keepResponse = JsonSerializer.Serialize(input.Select(o => Materialize(o)));
        var responses = new Queue<string>(new[] { keepResponse });

        var sut = BuildHandler(responses);
        var pipeline = NewPipelineWithSkill();
        pipeline.Set<List<SkillObservation>>(ContextKeys.SkillObservations, input);

        await sut.ExecuteAsync(NewContext(pipeline), CancellationToken.None);

        var stored = pipeline.Get<List<SkillObservation>>(ContextKeys.SkillObservations);
        stored.Should().HaveCount(1);
        // Details preservation: the LLM returned the observation with its Details field
        // (Materialize includes Details). Real filter prompts won't include Details, but
        // the parser-and-pipeline mechanically preserves whatever the LLM emits. This test
        // documents that round-trip works when the LLM honors Details — and that the new
        // Details field flows through end-to-end.
        stored[0].Details.Should().Contain("long-form body content");
    }

    private static FilterRoundHandler BuildHandler(Queue<string> responses)
    {
        var stub = new StubChatClient(responses);
        var factory = new StubChatClientFactory(stub);
        var promptBuilder = new Mock<ISkillPromptBuilder>();
        promptBuilder.Setup(p => p.BuildStructuredPromptParts(
            It.IsAny<RoleSkillDefinition>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
            It.IsAny<SkillRole?>(), It.IsAny<PlanArtifact?>()))
            .Returns(("system-prompt", "user-prefix", "user-suffix"));
        return new FilterRoundHandler(factory, promptBuilder.Object,
            NullLogger<FilterRoundHandler>.Instance);
    }

    private static FilterRoundContext NewContext(PipelineContext pipeline) =>
        new("false-positive-filter", 1, new AgentConfig { Type = "test" }, pipeline);

    private static PipelineContext NewPipelineWithSkill()
    {
        var pipeline = new PipelineContext();
        var role = new RoleSkillDefinition
        {
            Name = "false-positive-filter",
            DisplayName = "False Positive Filter",
            Emoji = "🛡️"
        };
        pipeline.Set<IReadOnlyList<RoleSkillDefinition>>(
            ContextKeys.AvailableRoles, new List<RoleSkillDefinition> { role });
        return pipeline;
    }

    private static List<SkillObservation> MakeObservations(int count) =>
        Enumerable.Range(0, count)
            .Select(i => ObservationFactory.Make("HIGH", $"src/a{i}.cs", i, $"finding {i}", "fix it", 80))
            .ToList();

    private static object Materialize(SkillObservation o) => new
    {
        concern = o.Concern.ToString().ToLowerInvariant(),
        description = o.Description,
        suggestion = o.Suggestion,
        severity = o.Severity.ToString().ToLowerInvariant(),
        confidence = o.Confidence,
        blocking = o.Blocking,
        details = o.Details,
        file = o.File,
        start_line = o.StartLine,
    };
}
