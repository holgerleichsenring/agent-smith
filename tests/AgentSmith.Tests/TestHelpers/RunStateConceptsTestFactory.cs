using AgentSmith.Contracts.Activation;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Infrastructure.Core.Services;
using AgentSmith.Infrastructure.Services.Activation;
using AgentSmith.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.TestHelpers;

/// <summary>
/// Test-only factory delegates for IRunStateConcepts that mirror the production
/// Func&lt;PipelineContext, IRunStateConcepts&gt; registration shape.
///
/// p0125c-followup: <see cref="Default"/> now loads the REAL concept-vocabulary.yaml
/// from the adjacent agent-smith-skills checkout via the SAME
/// <see cref="ConceptVocabularyLoader"/> production uses. The previous hand-rolled
/// vocabulary diverged silently from the shipped file (legal-analysis missing,
/// init-project missing, project_language missing, ...) — that drift is exactly
/// what hid the bug where PipelineNameInitializer fired with
/// ConceptVocabulary.Empty against a freshly-extracted catalog. Tests now exercise
/// the same vocabulary the binary will see at runtime.
///
/// When the agent-smith-skills checkout isn't reachable, falls back to a hand-
/// rolled minimal vocabulary so isolated CI / sandbox runs still get a working
/// factory; tests that need full coverage check
/// <see cref="TestSkillsRoot.IsAvailable"/> first.
/// </summary>
internal static class RunStateConceptsTestFactory
{
    /// <summary>Backed by PipelineContextRunStateConcepts with the supplied vocabulary.</summary>
    public static Func<PipelineContext, IRunStateConcepts> WithVocabulary(ConceptVocabulary vocabulary) =>
        ctx => new PipelineContextRunStateConcepts(ctx, vocabulary);

    private static readonly Lazy<ConceptVocabulary> RealVocabulary = new(LoadRealVocabularyOrFallback);

    /// <summary>
    /// The actual concept-vocabulary.yaml shipped with agent-smith-skills — same
    /// file the binary loads at pipeline-start in production.
    /// </summary>
    public static ConceptVocabulary Real => RealVocabulary.Value;

    /// <summary>
    /// Hand-rolled minimal fallback covering only the four p0125c concepts —
    /// used when the skills checkout isn't reachable. Tests asserting concrete
    /// concept-values should prefer <see cref="Real"/> + an
    /// <see cref="TestSkillsRoot.IsAvailable"/> guard.
    /// </summary>
    public static ConceptVocabulary FallbackMinimal { get; } = new(new Dictionary<string, ProjectConcept>
    {
        ["pipeline_name"] = new(
            "pipeline_name", "test", ConceptType.Enum,
            new[] { "api-security-scan", "security-scan", "fix-bug", "feature-implementation",
                    "mad-discussion", "legal-analysis", "init-project",
                    "autonomous", "skill-manager" },
            null, []),
        ["source_available"] = new("source_available", "test", ConceptType.Bool, null, null, []),
        ["context_yaml_present"] = new("context_yaml_present", "test", ConceptType.Bool, null, null, []),
        ["coding_principles_present"] = new("coding_principles_present", "test", ConceptType.Bool, null, null, []),
        ["project_language"] = new(
            "project_language", "test", ConceptType.Enum,
            new[] { "csharp", "node", "python", "generic" }, null, []),
    });

    /// <summary>Default factory — production-equivalent vocabulary when reachable, fallback otherwise.</summary>
    public static Func<PipelineContext, IRunStateConcepts> Default { get; } = WithVocabulary(Real);

    private static ConceptVocabulary LoadRealVocabularyOrFallback()
    {
        var skillsRoot = TestSkillsRoot.Resolve();
        if (skillsRoot is null) return FallbackMinimal;

        var loader = new ConceptVocabularyLoader(NullLogger<ConceptVocabularyLoader>.Instance);
        var loaded = loader.Load(skillsRoot);

        // Defense in depth: if the loader returned Empty (file disappeared
        // between IsAvailable and Load, or some yaml-parsing edge case),
        // fall back to the hand-rolled minimal so Default never produces
        // a vocab without pipeline_name. Empty would re-create the very
        // class of bug this fix is preventing.
        return loaded.Concepts.Count == 0 ? FallbackMinimal : loaded;
    }
}
