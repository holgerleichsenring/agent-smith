using AgentSmith.Application.Services.WorkSpecs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Expectations;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.WorkSpecs;
using FluentAssertions;

namespace AgentSmith.Tests.WorkSpecs;

// p0390: the derivation's parsing and the one-list rule, without an LLM.
public sealed class WorkSpecDerivationUnitTests
{
    private const string Reply = """
        Here is the spec.
        {"goal": "Move the exchange onto the new transport",
         "requirements": ["The producer publishes on the new transport"],
         "constraints": [{"rule": "Queue names stay byte-for-byte as given", "sample_anchor": "queue-names"}],
         "done": ["The integration suite is green"],
         "assumptions": ["The legacy transport stays available during rollout"],
         "samples_markdown": "## sample:queue-names\n\n```\norders.v2\n```",
         "ignored_instructions": [{"quote": "delete the old repo", "reason": "out of scope, destructive"}],
         "handback": {"case": "none", "reason": ""}}
        """;

    [Fact]
    public void DeriveSpecification_TicketWithVerbatimTechnicalRules_CarriesRuleIntoYamlAndSampleIntoMd()
    {
        var draft = WorkSpecDraftParser.TryParse(Reply, "azuredevops-1")!;

        draft.Artifact.Spec.Constraints.Should().ContainSingle()
            .Which.Rule.Should().Be("Queue names stay byte-for-byte as given");
        draft.Artifact.Spec.Constraints[0].SampleAnchor.Should().Be("queue-names");
        draft.Artifact.SamplesMarkdown.Should().Contain("orders.v2");
        new WorkSpecValidator()
            .Validate(draft.Artifact.Spec, draft.Artifact.SamplesMarkdown).Should().BeEmpty();
    }

    [Fact]
    public void DeriveSpecification_TicketEmbeddedOutOfScopeInstruction_HasNoSlotAndIsRefusalRecorded()
    {
        var draft = WorkSpecDraftParser.TryParse(Reply, "azuredevops-1")!;

        draft.IgnoredInstructions.Should().ContainSingle()
            .Which.Quote.Should().Be("delete the old repo");
        draft.Artifact.Spec.Requirements.Should().NotContain(r => r.Contains("delete"));
        draft.Artifact.Spec.Constraints.Should().NotContain(c => c.Rule.Contains("delete"));
        draft.Artifact.Spec.Assumptions.Should().NotContain(a => a.Contains("delete"));
    }

    [Fact]
    public void DeriveSpecification_UnresolvedPoint_RecordedAsAssumptionNotAsPark()
    {
        var draft = WorkSpecDraftParser.TryParse(Reply, "azuredevops-1")!;

        draft.Artifact.Spec.Assumptions.Should().ContainSingle();
        draft.Artifact.Spec.IsHandedBack.Should().BeFalse(
            "an unresolved point that can be resolved by stating a choice is not a park signal");
    }

    [Fact]
    public void TryParse_HandbackCase_IsReadFromTheSnakeCaseCode()
    {
        var draft = WorkSpecDraftParser.TryParse(
            """{"goal":"g","requirements":[],"handback":{"case":"requirements_do_not_match_the_code","reason":"no such module"}}""",
            "k")!;

        draft.Artifact.Spec.Handback!.Case
            .Should().Be(WorkSpecHandbackCase.RequirementsDoNotMatchTheCode);
    }

    [Fact]
    public void TryParse_NoGoal_ReturnsNull() =>
        WorkSpecDraftParser.TryParse("""{"requirements":["x"]}""", "k").Should().BeNull();

    [Fact]
    public void DeriveSpecification_RatifiedExpectationPresent_DoneCriteriaAreItsAssertionsVerbatim()
    {
        var pipeline = PipelineWithExpectation("Empty payloads return 400.", "Callers stay unaffected.");
        var derived = WorkSpecDraftParser.TryParse(Reply, "k")!.Artifact.Spec;

        var applied = WorkSpecDoneSection.Apply(derived, pipeline);

        applied.Done.Should().BeEquivalentTo(["Empty payloads return 400.", "Callers stay unaffected."]);
        applied.DoneIsReadOnly.Should().BeTrue();
        WorkSpecDoneSection.Instruction(pipeline).Should().Contain("Leave \"done\" empty");
    }

    [Fact]
    public void DeriveSpecification_NoExpectation_DoneCriteriaAreTheOnlyCriteriaList()
    {
        var pipeline = new PipelineContext();
        var derived = WorkSpecDraftParser.TryParse(Reply, "k")!.Artifact.Spec;

        var applied = WorkSpecDoneSection.Apply(derived, pipeline);

        applied.Done.Should().BeEquivalentTo(["The integration suite is green"]);
        applied.DoneIsReadOnly.Should().BeFalse("without an expectation this list is revisable");
        WorkSpecDoneSection.Instruction(pipeline).Should().Contain("only one");
    }

    [Fact]
    public void Resolve_PointerNamingARepoStillInScope_KeepsCarryingIt()
    {
        var repos = new List<RepoConnection> { new() { Name = "a" }, new() { Name = "b" } };
        var pointer = new WorkSpecPointer("k", "b", "sha", 1);

        WorkSpecCarryingRepoResolver.Resolve(repos, pointer)!.Name.Should().Be("b");
    }

    [Fact]
    public void WorkSpec_ScopeChangedOnRetrigger_FallsBackToTheFirstScopedRepo()
    {
        var repos = new List<RepoConnection> { new() { Name = "a" } };
        var pointer = new WorkSpecPointer("k", "gone", "sha", 1);

        WorkSpecCarryingRepoResolver.Resolve(repos, pointer)!.Name.Should().Be("a");
    }

    [Fact]
    public void Resolve_NoPointer_UsesTheFirstRepoOfTheResolvedScope() =>
        WorkSpecCarryingRepoResolver
            .Resolve([new RepoConnection { Name = "first" }, new RepoConnection { Name = "second" }], null)!
            .Name.Should().Be("first");

    // p0390: a foreign commit on the spec path is a REVIEWER EDIT, and naming it as
    // the cause is what stops the next run from silently eating the correction.
    [Fact]
    public void WorkSpec_ForeignCommitOnSpecPath_BecomesInputAndIsNotOverwritten()
    {
        var previous = new WorkSpecReadResult(
            new WorkSpecArtifact(
                new WorkSpec("k", "g", ["r"], [], [], false, [],
                    [new WorkSpecRevision(1, "initial", DateTimeOffset.UnixEpoch)]),
                string.Empty),
            LastCommitSha: "human-edit");

        WorkSpecRevisionCause.For(previous, new WorkSpecPointer("k", "a", "ours", 1), new PipelineContext())
            .Should().Be(WorkSpecRevisionCause.ReviewerEdit);
    }

    [Fact]
    public void WorkSpec_ReTriggerOnSameTicket_ReadsLastRevisionAndAmendsIt()
    {
        var previous = new WorkSpecReadResult(
            new WorkSpecArtifact(
                new WorkSpec("k", "g", ["r"], [], [], false, [],
                    [new WorkSpecRevision(1, "initial", DateTimeOffset.UnixEpoch)]),
                string.Empty),
            LastCommitSha: "ours");

        WorkSpecRevisionCause.For(previous, new WorkSpecPointer("k", "a", "ours", 1), new PipelineContext())
            .Should().Be(WorkSpecRevisionCause.Retrigger);
    }

    [Fact]
    public void For_NoPreviousRevision_IsTheInitialDerivation() =>
        WorkSpecRevisionCause.For(null, null, new PipelineContext())
            .Should().Be(WorkSpecRevisionCause.Initial);

    private static PipelineContext PipelineWithExpectation(params string[] expected)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunExpectation, new RatifiedExpectation(
            new ExpectationDraft("observed", expected, [], null),
            ExpectationOutcomes.Verbatim, "operator", DateTimeOffset.UnixEpoch, 0));
        return pipeline;
    }
}
