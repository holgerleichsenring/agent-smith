using AgentSmith.Application.Services.Events;
using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Events;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Specs.Review;

/// <summary>2026-10-13-8762e: a round stored as one event 87 per reviewer answer, only inside a run.</summary>
public sealed class SpecReviewRoundPublisherTests
{
    [Fact]
    public async Task SpecReviewRoundPublisher_PublishesOneEvent87PerAnswer_CutsCarrySpecId()
    {
        var events = new RecordingEventPublisher();
        var runContext = new AsyncLocalRunContextAccessor();
        var cut = new SpecReviewCut(SpecRegisterNames.Done, "lands in the export file", null, "shop/src/A.cs:3 [A1]",
            "2026-10-14-1234b");
        var round = new SpecReviewRound(null, SpecReviewScopes.Series, 2,
            [new SpecEvidenceProblem("claim", "not a file: src/B.cs")], [new SpecReviewResidue("goal", "four words of goal")], 5);

        using (runContext.BeginScope("run-1"))
            await new SpecReviewRoundPublisher(events, runContext, NullLogger<SpecReviewRoundPublisher>.Instance)
                .PublishAsync(round, [new SpecAttackResult(SpecReviewVerdicts.BuildWithCuts, [cut], []), null,
                    new SpecAttackResult(SpecReviewVerdicts.Build, [], [])], CancellationToken.None);

        var published = events.Events.Cast<SpecReviewRoundEvent>().ToList();
        published.Select(e => (e.Reviewer, e.Verdict)).Should().Equal(
            (0, SpecReviewVerdicts.BuildWithCuts), (2, SpecReviewVerdicts.Build));
        published.Should().OnlyContain(e => e.Type == EventType.SpecReviewRound && e.RunId == "run-1"
            && e.SpecId == null && e.Round == 2 && e.RevisionNumber == 5 && e.EvidenceProblems.Count == 1
            && e.Residues.Count == 1);
        published[0].Cuts.Single().SpecId.Should().Be("2026-10-14-1234b");
    }

    [Fact]
    public async Task SpecReviewRoundPublisher_OutsideARun_PublishesNothing()
    {
        var events = new RecordingEventPublisher();

        await new SpecReviewRoundPublisher(events, new AsyncLocalRunContextAccessor(),
                NullLogger<SpecReviewRoundPublisher>.Instance)
            .PublishAsync(new SpecReviewRound("s", SpecReviewScopes.Spec, 1, [], []),
                [new SpecAttackResult(SpecReviewVerdicts.Build, [], [])], CancellationToken.None);

        events.Events.Should().BeEmpty();
    }
}
