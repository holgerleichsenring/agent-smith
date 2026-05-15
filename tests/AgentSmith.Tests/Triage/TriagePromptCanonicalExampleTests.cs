using AgentSmith.Application.Services.Triage;
using AgentSmith.Contracts.Models.Skills;
using FluentAssertions;

namespace AgentSmith.Tests.Triage;

/// <summary>
/// p0138: closes the "example contradicts its own constraints" bug class
/// (the pre-p0138 prompt's JSON example cited frontmatter fields removed
/// by p0131a and used rationale keys not declared in concept-vocabulary
/// — the LLM imitated the example and produced invalid output).
///
/// The test runs the canonical example from triage-structured-system.md
/// through TriageOutputValidator with a synthetic skill set + vocabulary
/// matching it. If the validator rejects, the prompt is teaching the LLM
/// to produce invalid output.
/// </summary>
public sealed class TriagePromptCanonicalExampleTests
{
    [Fact]
    public void CanonicalExample_PassesTriageOutputValidator()
    {
        var output = BuildCanonicalExample();
        var skills = BuildSkillsCitedByExample();
        var vocabulary = BuildVocabularyCitedByExample();
        var validator = new TriageOutputValidator(new TriageRationaleParser());

        var result = validator.Validate(output, skills, vocabulary);

        result.IsValid.Should().BeTrue(
            "the example in triage-structured-system.md must itself satisfy the validator — " +
            "otherwise the LLM is being taught to produce invalid output. " +
            "Errors: " + string.Join("; ", result.Errors));
    }

    private static TriageOutput BuildCanonicalExample() => new(
        Phases: new Dictionary<PipelinePhase, PhaseAssignment>
        {
            [PipelinePhase.Plan] = new(
                Lead: "architect-planner",
                Analysts: new[] { "security-reviewer-investigator" },
                Reviewers: Array.Empty<string>(),
                Filter: null),
            [PipelinePhase.Review] = new(
                Lead: null,
                Analysts: Array.Empty<string>(),
                Reviewers: new[] { "architect-judge" },
                Filter: null),
            [PipelinePhase.Final] = new(
                Lead: null,
                Analysts: Array.Empty<string>(),
                Reviewers: Array.Empty<string>(),
                Filter: "false-positive-filter"),
        },
        Confidence: 85,
        Rationale: "lead=architect-planner:persistence;" +
                   "analyst=security-reviewer-investigator:authentication;" +
                   "reviewer=architect-judge:persistence;" +
                   "filter=false-positive-filter:authentication;");

    private static IReadOnlyList<SkillIndexEntry> BuildSkillsCitedByExample() =>
    [
        new("architect-planner", "Architectural standard-setter.", "producer", "plan", null),
        new("security-reviewer-investigator", "Security perspective.", "investigator", "observation", null),
        new("architect-judge", "Architecture review.", "judge", "observation", null),
        new("false-positive-filter", "Final-phase noise reducer.", "filter", "observation", null),
    ];

    private static ConceptVocabulary BuildVocabularyCitedByExample() =>
        new(new Dictionary<string, ProjectConcept>
        {
            ["persistence"] = new(
                Name: "persistence",
                Description: "Project stores state durably.",
                Type: ConceptType.Bool,
                EnumValues: null,
                IntRange: null,
                Writers: Array.Empty<string>()),
            ["authentication"] = new(
                Name: "authentication",
                Description: "Project verifies identity.",
                Type: ConceptType.Bool,
                EnumValues: null,
                IntRange: null,
                Writers: Array.Empty<string>()),
        });
}
