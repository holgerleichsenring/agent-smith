using AgentSmith.Application.Services.Pipeline;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-11-27-3532d: a retired spec stays a member of its set; the member whose supersedes names it takes its place. What
/// runs, what counts as the executed head, the progress table and the order all read the retirement off the members.
/// </summary>
public sealed class SpecSupersessionTests
{
    internal static DerivedSpec Spec(string id, IReadOnlyList<string>? requires = null, IReadOnlyList<string>? supersedes = null) =>
        new(new SpecDraft(id, $"goal {id}", $"spec: {id}\ngoal: \"goal {id}\"", requires ?? []) { Supersedes = supersedes ?? [] },
            "label", string.Empty, []);

    internal static SpecSet Set(IReadOnlyList<DerivedSpec> specs, IReadOnlyList<string>? executed = null) =>
        new("shop-42", specs, SpecAccounting.Empty, [new SpecRevision(1, "initial", DateTimeOffset.UtcNow)],
            SpecSource.BranchArtifact, ExecutedSpecIds: executed ?? [], Series: "2026-11-27-aaaa");

    // a retired for c; b required a and now requires c.
    private static IReadOnlyList<DerivedSpec> Retired() =>
        [Spec("x"), Spec("a"), Spec("c", supersedes: ["a"]), Spec("b", requires: ["c"])];

    [Fact]
    public void UnexecutedTail_ARetiredSpec_IsLeftOut()
    {
        var set = Set(Retired(), executed: ["x"]);

        set.UnexecutedTail.Select(s => s.SpecId).Should().Equal("c", "b");
        set.Planned().Select(s => s.SpecId).Should().Equal("a", "c", "b");
        set.Retired().Should().Equal("a");
    }

    [Fact]
    public void ExecutedHead_ARetiredSpecTheReplacementRanAfter_StaysInTheHead()
    {
        var set = Set(Retired(), executed: ["x", "c"]);

        set.ExecutedHead.Select(s => s.SpecId).Should().Equal("x", "a", "c");
    }

    [Fact]
    public void Render_ARetiredSpec_StaysInPlanned()
    {
        var files = new SeriesFiles().Render(Set(Retired(), executed: ["x"]));

        files.Select(f => f.Path).Should().Contain(SeriesPaths.Spec(SeriesPaths.Planned, "a-label"),
            "the retired spec's text stays on the branch beside its replacement");
        new SeriesStaleFiles().Select([SeriesPaths.Spec(SeriesPaths.Planned, "a-label")], Set(Retired(), executed: ["x"]), files)
            .Should().BeEmpty();
    }

    [Fact]
    public void Checkpoint_ARetiredSetRoundTrips_WithItsSupersedes()
    {
        var context = new PipelineContext();
        context.Set(ContextKeys.SpecSet, Set(Retired(), executed: ["x"]));
        context.Set(ContextKeys.SpecSequenceProgress, SpecSequenceProgress.ForSet(Set(Retired(), executed: ["x"])));
        var serializer = new AgentSmith.Application.Services.Resume.PipelineContextSerializer(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AgentSmith.Application.Services.Resume.PipelineContextSerializer>.Instance);

        var resumed = new PipelineContext();
        serializer.Restore(serializer.Serialize(context), resumed);

        var set = resumed.Get<SpecSet>(ContextKeys.SpecSet);
        set.Retired().Should().Equal("a");
        set.Specs[2].Draft.Supersedes.Should().Equal("a");
        resumed.Get<SpecSequenceProgress>(ContextKeys.SpecSequenceProgress).Specs[1].State.Should().Be(SpecRunState.Superseded);
    }

    [Fact]
    public void SeriesCriteria_ARetiredSpec_IsLeftOut()
    {
        IReadOnlyList<DerivedSpec> specs = [.. Retired().Select(s => s with { Draft = s.Draft with { Done = [$"{s.SpecId} holds"] } })];

        AgentSmith.Application.Services.Series.SeriesCriteria.Of("#42", Set(specs)).Select(c => c.Text)
            .Should().Equal("x holds", "c holds", "b holds");
    }

    [Fact]
    public void Merge_ARetiredSpecRanSinceTheApproval_DiscardsTheReplacementAndRelinksBack()
    {
        // The approval retired a for c and re-cut b to require c; a ran on the branch meanwhile.
        var branch = Set([Spec("x"), Spec("a"), Spec("b", requires: ["a"])], executed: ["x", "a"]);
        var approved = Set([Spec("c", supersedes: ["a"]), Spec("b", requires: ["c"])]);
        var continuation = new SeriesContinuation([new SpecDisposition("x", SpecDispositionKinds.Kept),
            new SpecDisposition("a", SpecDispositionKinds.Kept), new SpecDisposition("c", SpecDispositionKinds.Appended),
            new SpecDisposition("b", SpecDispositionKinds.Recut)], "r-1");

        var (specs, notes) = new ContinuationMerge(new AgentSmith.Application.Services.SpecDialog.SpecDraftIdRewriter())
            .Apply(branch, approved, continuation);

        specs.Select(s => s.SpecId).Should().Equal("x", "a", "b");
        specs[2].Draft.Requires.Should().ContainSingle().Which.Should().Be("a", "the discarded replacement's edge goes back to the spec that ran");
        notes.Should().Contain(n => n.Contains("its retirement is discarded with its replacement c"));
    }

    [Fact]
    public void Merge_ARetirement_PlacesTheReplacementBehindTheRetiredSpec()
    {
        var branch = Set([Spec("x"), Spec("a"), Spec("b", requires: ["a"])], executed: ["x"]);
        var approved = Set([Spec("a"), Spec("c", supersedes: ["a"]), Spec("b", requires: ["c"])]);
        var continuation = new SeriesContinuation([new SpecDisposition("x", SpecDispositionKinds.Kept),
            new SpecDisposition("a", SpecDispositionKinds.Kept), new SpecDisposition("c", SpecDispositionKinds.Appended),
            new SpecDisposition("b", SpecDispositionKinds.Recut)], "r-1");

        var (specs, _) = new ContinuationMerge(new AgentSmith.Application.Services.SpecDialog.SpecDraftIdRewriter())
            .Apply(branch, approved, continuation);

        specs.Select(s => s.SpecId).Should().Equal("x", "a", "c", "b");
        specs[3].Draft.Requires.Should().Equal("c");
    }

    [Fact]
    public void Markdown_ARetiredSpec_IsTaggedSupersededAndListsNoOpenCriteria()
    {
        IReadOnlyList<DerivedSpec> specs = [.. Retired().Select(s => s with { Draft = s.Draft with { Done = [$"{s.SpecId} holds"] } })];

        var markdown = SpecMarkdown.Render(Set(specs));

        markdown.Should().Contain("- **a** — goal a _(superseded by c)_").And.NotContain("a holds");
    }

    [Fact]
    public void Merge_ASpecARunRetiredMeanwhile_DiscardsTheApprovalsReplacement()
    {
        // A run retired a for r after the approval retired a for c: r stands, c is discarded and b requires r.
        var branch = Set([Spec("x"), Spec("a"), Spec("b", requires: ["a"]), Spec("r", supersedes: ["a"])], executed: ["x"]);
        var approved = Set([Spec("a"), Spec("c", supersedes: ["a"]), Spec("b", requires: ["c"])]);
        var continuation = new SeriesContinuation([new SpecDisposition("x", SpecDispositionKinds.Kept),
            new SpecDisposition("a", SpecDispositionKinds.Kept), new SpecDisposition("c", SpecDispositionKinds.Appended),
            new SpecDisposition("b", SpecDispositionKinds.Recut)], "r-1");

        var (specs, notes) = new ContinuationMerge(new AgentSmith.Application.Services.SpecDialog.SpecDraftIdRewriter())
            .Apply(branch, approved, continuation);

        specs.Select(s => s.SpecId).Should().Equal("x", "a", "r", "b");
        specs[3].Draft.Requires.Should().Equal("r");
        notes.Should().Contain(n => n.Contains("was retired for r since the approval was given"));
    }

    [Fact]
    public void Merge_AReMintedReplacement_IsWhatTheReCutSpecRequires()
    {
        // The approval's replacement id e was taken on the branch meanwhile, so it is minted anew; b follows it.
        var branch = Set([Spec("x"), Spec("a"), Spec("b", requires: ["a"]), Spec("e")], executed: ["x"]);
        var approved = Set([Spec("a"), Spec("e", supersedes: ["a"]), Spec("b", requires: ["e"])]);
        var continuation = new SeriesContinuation([new SpecDisposition("x", SpecDispositionKinds.Kept),
            new SpecDisposition("a", SpecDispositionKinds.Kept), new SpecDisposition("e", SpecDispositionKinds.Appended),
            new SpecDisposition("b", SpecDispositionKinds.Recut)], "r-1");

        var (specs, _) = new ContinuationMerge(new AgentSmith.Application.Services.SpecDialog.SpecDraftIdRewriter())
            .Apply(branch, approved, continuation);

        const string minted = "2026-11-27-aaaaa";
        specs.Select(s => s.SpecId).Should().Equal("x", "a", minted, "b", "e");
        specs[3].Draft.Requires.Should().Equal(minted);
    }

    [Fact]
    public void ExecutedHead_NeverEndsOnARetiredSpec()
    {
        var set = Set(Retired(), executed: ["x"]);

        set.ExecutedHead.Select(s => s.SpecId).Should().Equal("x");
    }

    [Fact]
    public void StandIns_ReDerivedAfterARetirement_KeepWhatRanAndTheRetirement()
    {
        var previous = Set(Retired(), executed: ["x", "c"]);
        var ticket = new AgentSmith.Domain.Entities.Ticket(new AgentSmith.Domain.Models.TicketId("42"), "Widget", "body", null, "open", "test");
        var standIns = new SpecStandIns(new AgentSmith.Application.Services.SpecDialog.SpecDraftValidator(
            new AgentSmith.Application.Services.Validation.SpecSchemaProvider()), new AgentSmith.Application.Services.SpecDialog.SpecDraftReader(),
            new DerivedSpecYamlRenderer());

        var set = standIns.Build("shop-42", "2026-11-27-aaaa", ticket, TicketSegmenter.Segment("body"), previous, SpecSource.Derived, [],
            ["2026-11-27-aaaaa", "2026-11-27-aaaab"]);

        set.Specs.Take(3).Select(s => s.SpecId).Should().Equal("x", "a", "c");
        set.Executed.Should().Equal("x", "c");
        set.Retired().Should().Equal("a");
        set.UnexecutedTail.Should().ContainSingle().Which.SpecId.Should().Be("2026-11-27-aaaad", "the stand-in takes the letter past the head");
    }

    [Fact]
    public void ForSet_ARetiredSpec_IsSupersededAndNotPartial()
    {
        var progress = SpecSequenceProgress.ForSet(Set(Retired(), executed: ["x", "c", "b"]));

        progress.Specs.Select(p => p.State).Should().Equal(
            SpecRunState.Done, SpecRunState.Superseded, SpecRunState.Done, SpecRunState.Done);
        progress.IsPartial.Should().BeFalse("a retired spec is settled");
        RunShortfall.Of(progress with { Specs = [.. progress.Specs.Take(3), progress.Specs[3] with { State = SpecRunState.Failed }] },
            "red").Should().NotBeNull().And.Subject.As<RunShortfall>().NotDelivered.Select(p => p.SpecId).Should().Equal("b");
    }

    [Fact]
    public void Superseding_PutsTheReplacementRowBehindTheRetiredOne()
    {
        var progress = SpecSequenceProgress.ForSet(Set([Spec("a"), Spec("b")]));

        var table = progress.Superseding("a", "c", "goal c", "superseded by c");

        table.Specs.Select(p => (p.SpecId, p.State)).Should().Equal(
            ("a", SpecRunState.Superseded), ("c", SpecRunState.InProgress), ("b", SpecRunState.NotStarted));
        table.Specs[0].Note.Should().Be("superseded by c");
    }

    [Fact]
    public void Ordered_AReplacementWithALaterLetter_StandsBehindTheSpecItSupersedes()
    {
        IReadOnlyList<DerivedSpec> byId = [Spec("a"), Spec("b", requires: ["c"]), Spec("c", supersedes: ["a"]), Spec("d", supersedes: ["c"])];

        SpecSupersession.Ordered(byId).Select(s => s.SpecId).Should().Equal("a", "c", "d", "b");
    }

    [Fact]
    public void FreeWithin_EveryLetterTaken_IsNull()
    {
        IReadOnlyCollection<string> all = [.. Enumerable.Range(0, 26).Select(i => $"s{(char)('a' + i)}")];

        SeriesIdFactory.FreeWithin("s", 26, ["sa", "sb"]).Should().Be("sc");
        SeriesIdFactory.FreeWithin("s", 26, all).Should().BeNull();
    }

    [Fact]
    public void Stamp_ReplacesASupersedesBlockAfterTheSpecLine()
    {
        var yaml = "spec: 2026-11-27-aaaac\nsupersedes:\n  - old\ngoal: \"g\"";

        SpecSupersedesLine.Stamp(yaml, "2026-11-27-aaaaa").Should().Be("spec: 2026-11-27-aaaac\nsupersedes: [2026-11-27-aaaaa]\ngoal: \"g\"");
    }

    [Fact]
    public void Without_DropsOneSpecsAccounts()
    {
        var accounts = RunAccounts.Empty.With("a", [new SpecAccount("shop", [])]).With("b", [new SpecAccount("shop", [])]);

        accounts.Without("a").Specs.Select(s => s.SpecId).Should().Equal("b");
    }

    [Fact]
    public void Restamp_TheRetiredSpecsSteps_CarryTheReplacement()
    {
        var context = new PipelineContext();
        context.Set(ContextKeys.SpecSet, Set(Retired()));
        var commands = new LinkedList<PipelineCommand>([
            new PipelineCommand(CommandNames.CommitSpecWork) { SpecId = "a" },
            new PipelineCommand(CommandNames.VerifySpec) { SpecId = "a" },
            new PipelineCommand(CommandNames.SelectSpec) { SpecId = "b" },
            new PipelineCommand(CommandNames.CommitAndPR)]);

        SpecBlockRestamp.Apply(commands.First!, context);

        commands.Select(c => c.SpecId).Should().Equal("c", "c", "b", null);
    }
}
