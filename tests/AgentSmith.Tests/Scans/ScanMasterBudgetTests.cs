using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Tests.Handlers;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Scans;

/// <summary>
/// 2026-09-01-7df4: the scan surface ran at an iteration ceiling nobody chose — the handler
/// passed null and null fell through to the chat client's private 25 — and its coverage
/// re-drive replaced the first pass's findings instead of adding to them.
/// </summary>
public sealed class ScanMasterBudgetTests
{
    private const string Master = "api-security-master";

    private const string FirstPass =
        """[{"concern":"security","severity":"high","description":"src/A.cs:10: first-pass finding","file":"src/A.cs","start_line":10}]""";

    private const string DeeperPass =
        """[{"concern":"security","severity":"medium","description":"src/B.cs:20: deeper-pass finding","file":"src/B.cs","start_line":20}]""";

    [Fact]
    public async Task ScanMaster_UsesTheConfiguredIterationCeiling_NotTheClientDefault()
    {
        var loop = await RunScanAsync(readFloor: 0, FirstPass);

        loop.SeenRequests[0].MaxIterations.Should().Be(
            new AgentConfig().ScanMasterLoopIterations,
            "a null ceiling fell through to the chat client's private default");
        loop.SeenRequests[0].MaxIterations.Should().NotBe(25);
    }

    [Fact]
    public async Task ScanMaster_ConfiguredCeiling_IsReadableOnTheRun()
    {
        var context = MasterHandlerFixture.BuildContext(Master, scanMinSourceReads: 0);
        await Handler(new SequencedLoopRunner(FirstPass)).ExecuteAsync(context, CancellationToken.None);

        context.Pipeline.TryGet<int>(ContextKeys.ScanMasterIterationCeiling, out var ceiling)
            .Should().BeTrue("the next argument about the number starts from a run that says it");
        ceiling.Should().Be(new AgentConfig().ScanMasterLoopIterations);
    }

    [Fact]
    public async Task ScanMaster_CoverageRedrive_KeepsTheFirstPassFindings()
    {
        var context = MasterHandlerFixture.BuildContext(Master); // floor 6, 0 reads → re-drive
        await Handler(new SequencedLoopRunner(FirstPass, DeeperPass))
            .ExecuteAsync(context, CancellationToken.None);

        context.Pipeline.TryGet<string>(ContextKeys.MasterAnswer, out var answer).Should().BeTrue();
        answer.Should().Contain("first-pass finding", "the deeper pass opens on an empty transcript")
            .And.Contain("deeper-pass finding");
    }

    [Fact]
    public async Task ScanMaster_CoverageRedrive_IsRecordedInTheConversation()
    {
        var conversation = new MasterConversation();
        var loop = new SequencedLoopRunner(DeeperPass);
        var request = Request();
        conversation.Opened(request, Answer(FirstPass));

        await Redrive(loop).DriveAsync(
            request, "REVIEW PROMPT", conversation, _ => { },
            readCount: 0, readFloor: 6, CancellationToken.None);

        var thread = conversation.Thread();
        thread.Should().Contain(m => m.Text != null && m.Text.Contains("FULL surface"),
            "the drive that ran is part of the conversation it drove");
        thread.Should().Contain(m => m.Text != null && m.Text.Contains("deeper-pass finding"));
    }

    [Fact]
    public async Task ScanMaster_ThatDidNotNeedARedrive_IsUnchanged()
    {
        var context = MasterHandlerFixture.BuildContext(Master, scanMinSourceReads: 0);
        var loop = new SequencedLoopRunner(FirstPass, DeeperPass);

        await Handler(loop).ExecuteAsync(context, CancellationToken.None);

        loop.SeenRequests.Should().ContainSingle("at the floor there is no second pass");
        context.Pipeline.TryGet<string>(ContextKeys.MasterAnswer, out var answer).Should().BeTrue();
        answer.Should().Be(FirstPass, "a single pass's answer is published verbatim");
    }

    [Fact]
    public void Union_AnswersThatRestateTheSameFinding_KeepOneCopy()
    {
        var union = new MasterAnswerUnion(TolerantJsonParserFactory.CreateTolerant());

        var combined = union.Combine(
        [
            new MasterPassAnswer(ScanMasterPasses.Unanchored, FirstPass),
            new MasterPassAnswer(ScanMasterPasses.Coverage, FirstPass),
            new MasterPassAnswer(ScanMasterPasses.Reconciliation, DeeperPass),
        ]);

        combined.Answer.Should().Contain("first-pass finding").And.Contain("deeper-pass finding");
        combined.Origins.Values.Should().BeEquivalentTo(
            [ScanMasterPasses.Unanchored, ScanMasterPasses.Reconciliation],
            "a later pass restating a finding does not take its origin");
        union.Combine([new MasterPassAnswer(ScanMasterPasses.Unanchored, "no findings at all")])
            .Answer.Should().BeNull(
                "an answer that is not findings must stay itself so the merge can degrade on it");
    }

    [Fact]
    public void IsJsonArray_ATruncatedArray_IsNotOne()
    {
        ITolerantJsonParser parser = TolerantJsonParserFactory.CreateTolerant();

        parser.IsJsonArray(FirstPass).Should().BeTrue();
        parser.IsJsonArray("""[{"description":"cut off""").Should().BeFalse();
        parser.IsJsonArray("prose").Should().BeFalse();
    }

    private static async Task<SequencedLoopRunner> RunScanAsync(int readFloor, params string[] texts)
    {
        var loop = new SequencedLoopRunner(texts);
        await Handler(loop).ExecuteAsync(
            MasterHandlerFixture.BuildContext(Master, scanMinSourceReads: readFloor),
            CancellationToken.None);
        return loop;
    }

    private static AgenticMasterHandler Handler(IAgenticLoopRunner loop) =>
        MasterHandlerFixture.Build(
            loop, new MasterHandlerFixture.StubPromptCatalog(Master, "body"),
            masterSchema: "observation");

    private static ScanCoverageRedrive Redrive(IAgenticLoopRunner loop) =>
        new(loop, new ScanMasterPromptFactory(), NullLogger<ScanCoverageRedrive>.Instance);

    private static AgenticLoopRequest Request() =>
        new(new AgentConfig(), TaskType.Primary, "system", "REVIEW PROMPT", []);

    private static ChatResponse Answer(string text) =>
        new(new ChatMessage(ChatRole.Assistant, text))
        {
            Usage = new UsageDetails { InputTokenCount = 10, OutputTokenCount = 5 },
        };

    private sealed class SequencedLoopRunner(params string[] texts) : IAgenticLoopRunner
    {
        private readonly List<AgenticLoopRequest> _seen = [];
        private int _call;
        public IReadOnlyList<AgenticLoopRequest> SeenRequests => _seen;

        public Task<AgenticLoopResult> RunAsync(
            AgenticLoopRequest request, CancellationToken cancellationToken)
        {
            _seen.Add(request);
            var text = texts[Math.Min(_call, texts.Length - 1)];
            _call++;
            return Task.FromResult(new AgenticLoopResult(Answer(text), TimeSpan.FromSeconds(1)));
        }
    }
}
