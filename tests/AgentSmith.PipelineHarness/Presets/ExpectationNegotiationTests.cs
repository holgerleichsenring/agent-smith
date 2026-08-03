using AgentSmith.Contracts.Expectations;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Server.Services.Lifecycle;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// p0328: the expectation-negotiation proof, LLM-free through the REAL
/// composition. A fix-bug run drafts the Soll block (one scripted LLM call),
/// parks on the durable ratification ask; the orchestrator restarts; the
/// operator's EDITED block lands in the durable inbox; the resumed run parses
/// the edit back into the schema WITHOUT re-drafting (no LLM call), records
/// outcome=edited + edit distance on the RunExpectation row, and carries the
/// ratified assertions into the rest of the run (second park at Approval,
/// approved, run completes — one run record).
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class ExpectationNegotiationTests
{
    private const string Fixture = "agentsmith-dialogue.yml";

    internal const string DraftJson = """
        {"observed": "The endpoint returns 500 on empty payloads.",
         "expected": ["Empty payloads return 400.", "Existing callers stay unaffected."],
         "constraints": ["No new dependencies."],
         "open_question": null}
        """;

    private const string EditedAnswer = """
        ## Expected
        - [ ] Empty payloads return 422.
        - [ ] Existing callers stay unaffected.

        ## Constraints
        - No new dependencies.
        """;

    private static void AssertRatifiedEdited(string dbPath, string runId)
    {
        using var ctx = DurableDialogueTests.Db(dbPath);
        var expectation = ctx.RunExpectations.Single();
        expectation.RunId.Should().Be(runId);
        expectation.Outcome.Should().Be(ExpectationOutcomes.Edited);
        expectation.RatifiedBy.Should().Be("@operator");
        expectation.EditDistance.Should().BeGreaterThan(0);
        expectation.RatifiedJson.Should().Contain("Empty payloads return 422.",
            "the operator's edit is the ratified contract");
        expectation.DraftJson.Should().Contain("Empty payloads return 400.",
            "the original draft stays recorded for the p0329 metric");
    }
}
