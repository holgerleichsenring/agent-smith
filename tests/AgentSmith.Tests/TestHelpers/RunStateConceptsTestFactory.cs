using AgentSmith.Contracts.Activation;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Infrastructure.Services.Activation;

namespace AgentSmith.Tests.TestHelpers;

/// <summary>
/// Test-only factory delegates for IRunStateConcepts that mirror the production
/// Func&lt;PipelineContext, IRunStateConcepts&gt; registration shape. Saves every
/// handler test from re-declaring the same lambda + vocabulary.
/// </summary>
internal static class RunStateConceptsTestFactory
{
    /// <summary>Backed by PipelineContextRunStateConcepts with the supplied vocabulary.</summary>
    public static Func<PipelineContext, IRunStateConcepts> WithVocabulary(ConceptVocabulary vocabulary) =>
        ctx => new PipelineContextRunStateConcepts(ctx, vocabulary);

    /// <summary>Vocabulary covering every concept p0125c handlers publish — for test wiring.</summary>
    public static ConceptVocabulary P0125cVocabulary { get; } = new(new Dictionary<string, ProjectConcept>
    {
        ["pipeline_name"] = new(
            "pipeline_name", "test", ConceptType.Enum,
            new[] { "api-security-scan", "security-scan", "fix-bug", "feature-implementation", "mad-discussion" },
            null, []),
        ["source_available"] = new("source_available", "test", ConceptType.Bool, null, null, []),
        ["context_yaml_present"] = new("context_yaml_present", "test", ConceptType.Bool, null, null, []),
        ["coding_principles_present"] = new("coding_principles_present", "test", ConceptType.Bool, null, null, [])
    });

    /// <summary>Factory bound to <see cref="P0125cVocabulary"/>.</summary>
    public static Func<PipelineContext, IRunStateConcepts> Default { get; } = WithVocabulary(P0125cVocabulary);
}
