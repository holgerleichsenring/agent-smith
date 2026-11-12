using System.Collections.Concurrent;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using AgentSmith.Contracts.Turns;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.Handlers;
using AgentSmith.Tests.Specs;
using AgentSmith.Tests.TestHelpers;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using YamlDotNet.Serialization;

namespace AgentSmith.Tests.SpecDialog.ProposalReview;

/// <summary>
/// 2026-10-13-8762i: a design turn's proposal review as the product composes it — 2026-11-12-cd4ch: the one
/// advisory pass, its panel and series round — over a scripted attack reviewer and a scripted evidence check. The
/// whole-set cut reviewer answers clean unless a test says otherwise.
/// </summary>
internal sealed class ProposalReviewRig
{
    public const string Parent = "2026-10-14-9000a";
    public const string First = "2026-10-14-9000b";
    public const string Second = "2026-10-14-9000c";
    public const string Lone = "2026-10-14-7000a";
    public const string Done = "Every order of the day lands in the export file.";

    private readonly TaskCompletionSource _gathered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _inFlight;

    public PipelineContext Pipeline { get; } = new();

    /// <summary>2026-11-11-2bdfd: the turn's reply slot, seeded as the pipeline seeds it; a test writes its
    /// Questions as state_open_questions would.</summary>
    public SpecDialogReplySlot Slot { get; } = new();

    public ProposalReviewRig() => Pipeline.Set(ContextKeys.SpecDialogReplySlot, Slot);

    public static OpenQuestion Question(string question, string proposal, params string[] specIds) =>
        new(question, proposal, specIds);
    public SpecReviewSettings Settings { get; set; } = Of(rounds: 4);
    public Func<SpecAttackInput, SpecAttackResult?> OnReview { get; set; } = _ => Build();
    public Func<SeriesContractInput, SeriesContractReview> OnSeriesReview { get; set; } =
        _ => new SeriesContractReview(Build(), []);
    public List<SeriesContractInput> SeriesReviewed { get; } = [];
    public Func<SpecDraft, SpecRoundEvidenceResult> OnEvidence { get; set; } = _ => SpecRoundEvidenceResult.None;
    public SpecCutReview WholeSet { get; set; } = SpecCutReview.Clean;
    public ConcurrentQueue<SpecAttackInput> Reviewed { get; } = new();
    public int WholeSetCalls { get; private set; }
    public int MaxInFlight { get; private set; }
    public bool RealEvidence { get; init; }

    /// <summary>When above 0, every reviewer call waits until this many are in flight at once.</summary>
    public int GatherInFlight { get; init; }
    public AgenticLoopResult FirstReply { get; private set; } = Result("");

    /// <summary>2026-11-12-cd4ch: <paramref name="designTurn"/> is on unless a test turns the design turn's pass off.</summary>
    public static SpecReviewSettings Of(
        int rounds, int reviewers = 2, int maxConcurrent = 2, int share = 40, int wall = 1800, bool designTurn = true) =>
        new(new SpecReviewConfig
        {
            Rounds = rounds, Reviewers = reviewers, MaxConcurrent = maxConcurrent, BudgetSharePercent = share, DesignTurn = designTurn,
        }, wall);

    public static SpecAttackResult Build() => new(SpecReviewVerdicts.Build, [], []);

    public static SpecAttackResult Cuts(params SpecReviewCut[] cuts) => new(SpecReviewVerdicts.BuildWithCuts, cuts, []);

    /// <summary>A REFUSE that names a cut: applying it does not clear the draft, so another round follows.</summary>
    public static SpecAttackResult RefuseCuts(params SpecReviewCut[] cuts) =>
        new(SpecReviewVerdicts.Refuse, cuts, [new SpecReviewFinding("the writer is not buffered", "shop/src/Export/Writer.cs:9 [A1]")]);

    public static SpecAttackResult Refuse(string why) =>
        new(SpecReviewVerdicts.Refuse, [], [new SpecReviewFinding(why, "shop/src/Export/Writer.cs:9 [A1]")]);

    public static SpecReviewCut Cut(string register, string quote, string? replacement) =>
        new(register, quote, replacement, "shop/src/Export/Writer.cs:9 [A1]");

    public static string Yaml(string id, string goal = "The export writes every order of the day into one file.",
        string step = "Write the orders through the buffered file writer.", params string[] requires) => $$"""
        spec: {{id}}
        goal: "{{goal}}"
        applies_to: "backend (export)"
        requires: [{{string.Join(", ", requires)}}]
        scope:
          in: "The export handler and its file writer."
          out: "The import side stays as it is."
        steps:
          - id: writer
            action: "{{step}}"
        tests:
          - "Export_WritesEveryOrderOfTheDay"
        done:
          - "{{Done}}"
        facts:
          - claim: "The export handler reads the orders from the order store."
            evidence: "src/Export/ExportHandler.cs:12"
        """;

    public static string SpecReply(string yaml, string prose = "Here is the spec.") => $"{prose}\n\n```yaml\n{yaml}\n```\n";

    public static string EpicReply(
        string parent, IEnumerable<string> children, string prose = "Here is the epic.", object? contract = null)
    {
        var map = new Dictionary<object, object?>
        {
            ["kind"] = "epic",
            ["parent"] = Map(parent),
            ["children"] = children.Select(Map).ToList(),
        };
        if (contract is not null) map["contract"] = contract;
        return $"{prose}\n\n```outcome\n{OutcomeYamlReader.ToYaml(map)}```\n";
    }

    public static string Epic(string? first = null, string? second = null, string? parent = null) =>
        EpicReply(parent ?? Yaml(Parent, "The export is rebuilt in two slices."),
            [first ?? Yaml(First), second ?? Yaml(Second, requires: [First])]);

    /// <summary>2026-10-13-8762k: the two-child epic with a series contract — event 87 owned by the first child.</summary>
    public static string ContractEpic() => EpicReply(Yaml(Parent, "The export is rebuilt in two slices."),
        [Yaml(First), Yaml(Second, requires: [First])], contract: new Dictionary<object, object?>
        {
            ["names"] = new List<object?>
            {
                new Dictionary<object, object?> { ["name"] = "OrderExported", ["kind"] = "event", ["number"] = 87, ["owner"] = First },
            },
        });

    /// <summary>2026-11-10-18cf: the turn holds a repository, so a reviewer could have read it.</summary>
    public ProposalReviewRig WithRepository()
    {
        Pipeline.Set(ContextKeys.Sandboxes, (IReadOnlyDictionary<string, ISandbox>)new Dictionary<string, ISandbox>
        {
            ["shop"] = new EvidenceTreeSandbox(new Dictionary<string, string>()),
        });
        return this;
    }

    /// <summary>The proposal the gate admitted from <paramref name="reply"/>, published as the turn's outcome.</summary>
    public OutcomeProposal Propose(string reply)
    {
        var proposal = ((OutcomeResolved)MasterHandlerFixture.BuildOutcomeResolver().Resolve(reply, [])).Proposal;
        Pipeline.Set(ContextKeys.SpecDialogOutcome, proposal);
        FirstReply = Result(reply);
        return proposal;
    }

    public SpecDialogProposalReview Review(ISpecReviewSettingsReader? reader = null) =>
        new(ProposalEvidenceTestReview.Create(), reader ?? Reader(() => Settings), Pass(), TicketPlanReviewRig.Default(),
            NullLogger<SpecDialogProposalReview>.Instance, Proposals);

    public async Task<AgenticLoopResult> RunAsync(ISpecReviewSettingsReader? reader = null)
    {
        var request = new AgenticLoopRequest(new AgentConfig(), TaskType.Planning, "system", "user", []);
        var conversation = new MasterConversation();
        conversation.Opened(request, FirstReply.Response);
        return await Review(reader).ReviewAsync(Pipeline, request, "user", FirstReply, conversation, new AgentConfig(),
            PipelineCostTracker.GetOrCreate(Pipeline), CancellationToken.None);
    }

    /// <summary>2026-11-09-9a98: set by a test that watches what the review publishes as it goes.</summary>
    public ITurnProposalObserverAccessor? Proposals { get; init; }

    public OutcomeProposal Outcome => Pipeline.Get<OutcomeProposal>(ContextKeys.SpecDialogOutcome);

    public SpecDraft DraftOf(string specId) => ProposalSpecSet.Drafts(Outcome).Single(d => d.SpecId == specId);

    public static ISpecReviewSettingsReader Reader(Func<SpecReviewSettings> current)
    {
        var reader = new Mock<ISpecReviewSettingsReader>();
        reader.Setup(r => r.Current()).Returns(current);
        return reader.Object;
    }

    public static AgenticLoopResult Result(string text) =>
        new(new ChatResponse(new ChatMessage(ChatRole.Assistant, text)), TimeSpan.Zero);

    /// <summary>2026-11-12-cd4ch: the one advisory pass, as the post-turn review and the review tool run it.</summary>
    public ProposalReviewPass Pass() => Pass(WholeSetReviewer());

    /// <summary>The pass with <paramref name="wholeSet"/> as its whole-set reviewer, reporting to <paramref name="activity"/>.</summary>
    public ProposalReviewPass Pass(ISpecCutReviewer wholeSet, ITurnActivityObserverAccessor? activity = null) =>
        new(new ProposalWholeSetReview(wholeSet, DerivationTestLooks.Factory(activity: TurnActivityRecorder.Tools(activity)),
                NullLogger<ProposalWholeSetReview>.Instance),
            new ProposalAttackPass(
                new ProposalAttackPanel(Attacker(), DerivationTestLooks.Factory(), NullLogger<ProposalAttackPanel>.Instance),
                Evidence(), new ProposalSeriesRound(SeriesReviewer(), DerivationTestLooks.Factory(), NullLogger<ProposalSeriesRound>.Instance),
                NullLogger<ProposalAttackPass>.Instance),
            activity ?? TurnActivityRecorder.Silent());

    private ISeriesContractReviewer SeriesReviewer()
    {
        var reviewer = new Mock<ISeriesContractReviewer>();
        reviewer.Setup(r => r.ReviewAsync(It.IsAny<SeriesContractInput>(), It.IsAny<DerivationLook?>(),
                It.IsAny<AgentConfig>(), It.IsAny<PipelineCostTracker>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SeriesContractInput input, DerivationLook? _, AgentConfig _, PipelineCostTracker _, CancellationToken _) =>
            {
                SeriesReviewed.Add(input);
                return OnSeriesReview(input);
            });
        return reviewer.Object;
    }

    private ISpecAttackReviewer Attacker()
    {
        var reviewer = new Mock<ISpecAttackReviewer>();
        reviewer.Setup(r => r.ReviewAsync(It.IsAny<SpecAttackInput>(), It.IsAny<DerivationLook?>(),
                It.IsAny<AgentConfig>(), It.IsAny<PipelineCostTracker>(), It.IsAny<CancellationToken>()))
            .Returns(async (SpecAttackInput input, DerivationLook? _, AgentConfig _, PipelineCostTracker _, CancellationToken _) =>
            {
                var now = Interlocked.Increment(ref _inFlight);
                lock (Reviewed) MaxInFlight = Math.Max(MaxInFlight, now);
                if (GatherInFlight > 0 && now >= GatherInFlight) _gathered.TrySetResult();
                if (GatherInFlight > 0) await _gathered.Task.OrHang("reviewers in flight together");
                else await Task.Yield();
                Reviewed.Enqueue(input);
                Interlocked.Decrement(ref _inFlight);
                return OnReview(input);
            });
        return reviewer.Object;
    }

    private ISpecRoundEvidence Evidence()
    {
        if (RealEvidence)
            return new SpecRoundEvidence(new EvidenceCheck(new EvidenceReferences()), new EvidenceReferences(),
                TurnActivityRecorder.Silent());
        var evidence = new Mock<ISpecRoundEvidence>();
        evidence.Setup(e => e.CheckAsync(It.IsAny<PipelineContext>(), It.IsAny<SpecDraft>(), It.IsAny<SpecSet>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((PipelineContext _, SpecDraft draft, SpecSet _, CancellationToken _) => OnEvidence(draft));
        return evidence.Object;
    }

    private ISpecCutReviewer WholeSetReviewer()
    {
        var reviewer = new Mock<ISpecCutReviewer>();
        reviewer.Setup(r => r.ReviewAsync(It.IsAny<IReadOnlyList<SpecDraft>>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<DerivationLook?>(), It.IsAny<AgentConfig>(), It.IsAny<PipelineCostTracker>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                WholeSetCalls++;
                return WholeSet;
            });
        return reviewer.Object;
    }

    private static object? Map(string yaml) => new DeserializerBuilder().Build().Deserialize<object?>(yaml);
}
