using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Tests.TestHelpers;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Pipeline;

/// <summary>
/// p0125c-followup: regression-fence for the
/// "Concept X not declared in concept-vocabulary.yaml" class of bug. Drives
/// PipelineNameInitializer (step 1 of every preset) against the REAL
/// concept-vocabulary.yaml from the agent-smith-skills checkout.
///
/// Pre-fix this class would fail because PipelineNameInitializer fired
/// before LoadSkills against ConceptVocabulary.Empty. Post-fix the eager
/// vocab load in ExecutePipelineUseCase pre-populates the slot, so any
/// preset whose <c>pipeline_name</c> is in the enum should pass cleanly.
///
/// The test loads the SAME concept-vocabulary.yaml the production binary
/// loads — drift between test fixture and shipped catalog is the gap the
/// pre-fix test factory had, and the gap that hid this bug.
/// </summary>
public sealed class PipelinePresetSmokeTests
{
    private readonly PipelineNameInitializerHandler _handler = new(
        RunStateConceptsTestFactory.Default,
        NullLogger<PipelineNameInitializerHandler>.Instance);

    public static TheoryData<string> PipelineNameEnumValues()
    {
        var data = new TheoryData<string>();
        if (!TestSkillsRoot.IsAvailable())
        {
            data.Add("__skills_unavailable__");
            return data;
        }
        var vocab = RunStateConceptsTestFactory.Real;
        if (!vocab.Concepts.TryGetValue("pipeline_name", out var concept) || concept.EnumValues is null)
        {
            data.Add("__pipeline_name_not_in_vocab__");
            return data;
        }
        foreach (var value in concept.EnumValues) data.Add(value);
        return data;
    }

    [Theory, MemberData(nameof(PipelineNameEnumValues))]
    public async Task PipelineNameInitializer_AcceptsEveryEnumValue(string pipelineName)
    {
        if (pipelineName.StartsWith("__")) return; // skills checkout unavailable; degraded skip

        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ResolvedPipeline,
            new ResolvedPipelineConfig(pipelineName, new AgentConfig(), "skills/coding", null));

        var act = async () =>
            await _handler.ExecuteAsync(new PipelineNameInitializerContext(pipeline), CancellationToken.None);

        // Production runs the eager-load shortly before this handler;
        // the real vocab includes every pipeline_name enum value.
        await act.Should().NotThrowAsync(
            $"pipeline_name='{pipelineName}' is declared in the real vocabulary's pipeline_name enum");
    }

    // p0144: 'autonomous' + 'skill-manager' were fenced-by-design pre-p0144
    // (no skills with matching pipeline_name in the catalog → preset crashed
    // at this handler). agent-smith-skills v2.2.0 ships the vocab bump + 8
    // SKILL.md files that unfence them; the rejection assertion that lived
    // here is now covered by PipelineNameInitializerHandlerTests's positive
    // path assertions for both values.

    [Fact]
    public void RealVocabulary_ContainsEveryConceptPreSkillHandlersPublish()
    {
        // Architectural fence: pre-LoadSkills handlers (PipelineNameInitializer,
        // BootstrapCheckHandler, CheckoutSourceHandler / TryCheckoutSourceHandler,
        // PublishProjectLanguageHandler) all call SetBool / SetEnum on declared
        // concepts. Those concepts must exist in the shipped vocabulary or
        // pipeline-bootstrap fails fast. Adding a new IConceptWriter that runs
        // before LoadSkills? Add its concept to
        // agent-smith-skills/concept-vocabulary.yaml in the same cross-repo
        // change.
        if (!TestSkillsRoot.IsAvailable()) return;

        var vocab = RunStateConceptsTestFactory.Real;
        vocab.Concepts.Should().ContainKey("pipeline_name");
        vocab.Concepts.Should().ContainKey("source_available");
        vocab.Concepts.Should().ContainKey("context_yaml_present");
        vocab.Concepts.Should().ContainKey("coding_principles_present");
        vocab.Concepts.Should().ContainKey("project_language");
    }
}
