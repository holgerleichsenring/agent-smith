using AgentSmith.Application.Services.WorkSpecs;
using AgentSmith.Contracts.WorkSpecs;
using FluentAssertions;

namespace AgentSmith.Tests.WorkSpecs;

// p0390: hand-back is CASE-CODED so non-progress is mechanical. Comparing
// LLM-written reasons would never match — the same fact is written differently
// twice — so the comparison is the case code plus "was anything committed".
public sealed class WorkSpecHandbackTests
{
    private static WorkSpecPointer Pointer(
        WorkSpecHandbackCase last = WorkSpecHandbackCase.None,
        string? sourceSha = null,
        int repeats = 0) =>
        new("p-1", "primary", "sha", 1, last, repeats, sourceSha);

    [Fact]
    public void Handback_SameCaseCodeAndNoSourceCommit_StopsHandingBack() =>
        WorkSpecHandbackProgress.RepeatsWithoutProgress(
            Pointer(WorkSpecHandbackCase.NotUnderstood, "sha"),
            WorkSpecHandbackCase.NotUnderstood, "sha")
        .Should().BeTrue();

    [Fact]
    public void RepeatsWithoutProgress_SameCaseButSomethingWasCommitted_HandsBackAgain() =>
        WorkSpecHandbackProgress.RepeatsWithoutProgress(
            Pointer(WorkSpecHandbackCase.NotUnderstood, "sha"),
            WorkSpecHandbackCase.NotUnderstood, "newer-sha")
        .Should().BeFalse();

    [Fact]
    public void RepeatsWithoutProgress_DifferentCaseCode_HandsBackAgain() =>
        WorkSpecHandbackProgress.RepeatsWithoutProgress(
            Pointer(WorkSpecHandbackCase.NotUnderstood, "sha"),
            WorkSpecHandbackCase.NotImplementable, "sha")
        .Should().BeFalse();

    [Fact]
    public void RepeatsWithoutProgress_FirstHandbackEver_HandsBack() =>
        WorkSpecHandbackProgress.RepeatsWithoutProgress(
            null, WorkSpecHandbackCase.NotUnderstood, "sha").Should().BeFalse();

    [Fact]
    public void Record_SameCaseTwice_IncrementsTheRepeatCount() =>
        WorkSpecHandbackProgress.Record(
            Pointer(WorkSpecHandbackCase.NotUnderstood, "old", repeats: 1),
            WorkSpecHandbackCase.NotUnderstood, "new")
        .Should().BeEquivalentTo(new
        {
            LastHandbackCase = WorkSpecHandbackCase.NotUnderstood,
            RepeatedHandbackCount = 2,
            HandbackSourceSha = "new",
        });

    [Fact]
    public void Record_DifferentCase_ResetsTheRepeatCount() =>
        WorkSpecHandbackProgress.Record(
            Pointer(WorkSpecHandbackCase.NotUnderstood, "old", repeats: 3),
            WorkSpecHandbackCase.NotImplementable, "new")
        .RepeatedHandbackCount.Should().Be(1);

    // The verdict comment carries NO question anchor, so no comment on the ticket
    // can be parsed as an answer — that is what makes "does not auto-retry on a
    // comment" a structural property rather than a rule someone has to remember.
    [Fact]
    public void Handback_NotImplementable_DoesNotAutoRetryOnComment()
    {
        var body = WorkSpecHandbackComment.Build(
            new WorkSpecHandback(WorkSpecHandbackCase.NotImplementable, "the API does not exist"),
            prUrl: null);

        body.Should().NotContain("agent-smith:open-questions");
        body.Should().NotContain("[Q");
        body.Should().Contain("Retry");
        body.Should().Contain("the API does not exist");
    }

    [Fact]
    public void Build_QuestionCase_ReadsAsAQuestionNotAVerdict()
    {
        var body = WorkSpecHandbackComment.Build(
            new WorkSpecHandback(WorkSpecHandbackCase.RequirementsDoNotMatchTheCode, "no such module"),
            prUrl: "https://example.test/pr/1");

        body.Should().Contain("do not match the code");
        body.Should().NotContain("Retry");
        body.Should().Contain("https://example.test/pr/1");
    }
}
