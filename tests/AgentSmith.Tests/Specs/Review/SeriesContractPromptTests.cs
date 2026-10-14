using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using FluentAssertions;
using static AgentSmith.Tests.Specs.Review.SeriesReviewFixtures;

namespace AgentSmith.Tests.Specs.Review;

/// <summary>2026-10-13-8762k: what the series-contract reviewer is shown and how its answer is read.</summary>
public sealed class SeriesContractPromptTests
{
    [Fact]
    public void SeriesContractPrompt_CarriesContractAndEveryMember()
    {
        var prompt = SeriesContractPrompt.For(Input(), look: null);

        prompt.Should().Contain("## The series contract").And.Contain("event 'OrderExported' = 87, owner " + A)
            .And.Contain("datum: 'export file' lives in the export folder");
        prompt.Should().Contain($"### {A} (cleared)").And.Contain($"### {B} (cleared)").And.Contain(BGoal);
        prompt.Should().Contain("\"spec_id\"").And.NotContain("Shown in part");
    }

    [Fact]
    public void SeriesContractPrompt_Turn_AsksForTheContractEntryAsEvidence()
    {
        var prompt = SeriesContractPrompt.For(Input(inRun: false), look: null);

        prompt.Should().Contain("\"entry\"").And.Contain($"### {A}\n").And.NotContain("(cleared)");
    }

    [Fact]
    public void SeriesContractPrompt_TwentyFourMembers_StaysUnderTheCap()
    {
        var padding = string.Join("\n", Enumerable.Range(0, 60).Select(i => $"  - key: \"DECISION {i} keeps the export file readable by people.\""));
        var members = Enumerable.Range(0, 24).Select(i =>
        {
            var id = $"2026-10-14-12{i:00}a";
            var yaml = AttackReviewFixtures.Yaml.Replace($"spec: {A}", $"spec: {id}", StringComparison.Ordinal)
                .Replace("decisions:\n", "decisions:\n" + padding + "\n", StringComparison.Ordinal);
            return new SeriesContractMember(new SpecDraft(id, "goal", yaml, []), SeriesContractMember.Cleared);
        }).ToList();
        members.Sum(m => m.Draft.Yaml.Length).Should().BeGreaterThan(SeriesContractPrompt.MaxMembersChars, "the set does not fit whole");

        var prompt = SeriesContractPrompt.For(Input() with { Members = members }, look: null);

        prompt.Length.Should().BeLessThanOrEqualTo(SeriesContractPrompt.MaxMembersChars);
        prompt.Should().Contain("Shown in part");
        members.Should().AllSatisfy(m => prompt.Should().Contain($"### {m.Draft.SpecId} (cleared)"));
        prompt.Should().Contain("export file", "a done line naming a contract datum is kept when shown in part");
    }

    [Fact]
    public void SeriesContractAnswerReader_NullOrStringNumber_DoesNotThrow()
    {
        var answer = SeriesContractAnswerReader.Read("""
            Here: {"verdict": "build-with-cuts", "cuts": [{"spec_id": "x", "register": "Goal", "quote": "q", "line": null, "cites": ["A1", "A2"]},
                    {"spec_id": null, "line": "12", "cites": 3}], "findings": [{"text": "t", "entry": "name: event", "line": {"n": 1}}]}
            """);

        answer.Should().NotBeNull();
        answer!.Verdict.Should().Be(SpecReviewVerdicts.BuildWithCuts);
        answer.Cuts.Select(c => (c.SpecId, c.Item.Line)).Should().Equal(("x", 0), (null, 12));
        answer.Cuts[0].Item.Register.Should().Be("goal");
        answer.Cuts[0].Item.Cites.Should().Be("A1, A2");
        answer.Findings.Should().ContainSingle().Which.Entry.Should().Be("name: event");
    }

    [Fact]
    public void SeriesContractAnswerReader_NoVerdict_ReadsNothing() =>
        SeriesContractAnswerReader.Read("""{"verdict": "maybe", "cuts": []}""").Should().BeNull();
}
