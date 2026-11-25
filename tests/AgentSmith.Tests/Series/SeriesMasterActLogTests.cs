using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Series;
using AgentSmith.Contracts.Services;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Series;

/// <summary>2026-11-25-9992e: every act of the series master leaves a SeriesAct with its ticket and reason on the record; a CI
/// read is logged but keeps the beat a looking one; the master's surface stays within the house cap of 20 tools.</summary>
public sealed class SeriesMasterActLogTests
{
    private readonly InMemorySeriesRunStore _store = new();
    private readonly Mock<IAgenticLoopRunner> _loop = new();
    private readonly Mock<ISeriesTicketActs> _acts = new();
    private readonly Mock<ISeriesPullRequestActs> _pullRequests = new();
    private readonly Mock<ISeriesEditActs> _edits = new(); // 2026-11-25-9992f
    private readonly Mock<ISeriesConversation> _conversation = new();
    private IList<AITool> _tools = [];

    private SeriesMasterHandler Handler(ISeriesRecordReads? records = null) => new(_loop.Object,
        Mock.Of<IPromptCatalog>(p => p.Get(It.IsAny<string>()) == "body"), _acts.Object, _store, new AgentSmithConfig(), _conversation.Object,
        new SeriesProgress(_store, Mock.Of<ISeriesRunLaunch>()),
        new SeriesPause(_store, Mock.Of<ISeriesTicketFacts>(), _acts.Object, Mock.Of<ISeriesConversation>()),
        _pullRequests.Object, SeriesFinalDoubles.Merge(), SeriesFinalDoubles.Ci(), AgentSmith.Tests.TestSupport.SeriesWakeDoubles.Budget(_store), _edits.Object,
        NullLogger<SeriesMasterHandler>.Instance, records);

    private async Task<SeriesBeat> JudgeAsync(string? tool, string arguments = "{}", ISeriesRecordReads? records = null)
    {
        _conversation.Setup(c => c.AddressedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _loop.Setup(l => l.RunAsync(It.IsAny<AgenticLoopRequest>(), It.IsAny<CancellationToken>()))
            .Returns<AgenticLoopRequest, CancellationToken>(async (request, ct) =>
            {
                _tools = request.Tools;
                if (tool is not null)
                    await request.Tools.OfType<AIFunction>().Single(t => t.Name == tool)
                        .InvokeAsync(new AIFunctionArguments(System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object?>>(arguments)), ct);
                return new AgenticLoopResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "done")), TimeSpan.Zero);
            });
        await _store.OpenAsync(SeriesRecords.Running() with { LastStatusAt = SeriesRecords.T }, default);
        var beat = new SeriesBeat(_store.Records["sr-1"], [SeriesRecords.Fact("43", "running", finished: false)], SeriesRecords.T);
        beat.Reasons.Add("SCRUM-43 turned red");
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.SeriesBeat, beat);
        await Handler(records).ExecuteAsync(new SeriesMasterContext(pipeline, new ResolvedProject { Name = "sample" }), default);
        return beat;
    }

    [Fact]
    public async Task AnAct_LeavesItsTicketTextAndReasonOnTheRecord()
    {
        _acts.Setup(a => a.RetryAsync("sample", "43", "operator", It.IsAny<CancellationToken>())).ReturnsAsync("Retry requested.");

        var beat = await JudgeAsync("retry_ticket", """{"ticket":"SCRUM-43","reason":"the runner timed out, not the work"}""");

        beat.MasterActed.Should().BeTrue();
        _store.Records["sr-1"].Acts.Should().ContainSingle().Which.Should().Be(
            new SeriesAct(SeriesRecords.T, "retry_ticket", "43", "retried SCRUM-43", "the runner timed out, not the work"));
        SeriesTicketLastAct.Of(_store.Records["sr-1"].Acts, "43").Should().Be(
            new SeriesTicketLastAct("retried SCRUM-43", "the runner timed out, not the work", SeriesRecords.T));
    }

    [Fact]
    public async Task ACiRead_IsLogged_ButTheBeatOnlyLooked()
    {
        _pullRequests.Setup(a => a.CiAsync(It.IsAny<SeriesRunRecord>(), "43", SeriesCiActs.Read, It.IsAny<CancellationToken>())).ReturnsAsync("CI red");

        var beat = await JudgeAsync("ci", """{"ticket":"SCRUM-43","act":"read","reason":"is it red?"}""");

        beat.MasterActed.Should().BeFalse();
        _store.Records["sr-1"].Acts.Should().ContainSingle().Which.Should().Match<SeriesAct>(a => a.Text == "read the CI of SCRUM-43");
        beat.Said.Should().BeEmpty("a read is not said in the conversation");
    }

    // 2026-11-25-9992f: add_ticket names no ticket in its arguments; its act is the added ticket's.
    [Fact]
    public async Task AnAddedTicket_IsTheActsTicket()
    {
        _edits.Setup(e => e.AddTicketAsync(It.IsAny<SeriesRunRecord>(), "Index", "add it", It.IsAny<IReadOnlyList<string>>(),
            It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync(new SeriesCutAnswer("Filed SCRUM-46.", true, "46", "SCRUM-46"));

        await JudgeAsync("add_ticket", """{"title":"Index","body":"add it","requiredBy":["SCRUM-43"],"reason":"43 needs it"}""");

        SeriesTicketLastAct.Of(_store.Records["sr-1"].Acts, "46").Should().Be(new SeriesTicketLastAct("added SCRUM-46", "43 needs it", SeriesRecords.T));
    }

    [Fact]
    public void LastAct_IsTheNewestActNamingTheTicket()
    {
        IReadOnlyList<SeriesAct> acts =
        [
            new(SeriesRecords.T, "merge_pull_request", "43", "merged SCRUM-43 (now)", "green"),
            new(SeriesRecords.T.AddMinutes(1), "retry_ticket", "44", "retried SCRUM-44", null),
            new(SeriesRecords.T.AddMinutes(2), "refresh_branch", "43", "refreshed SCRUM-43", "it conflicts"),
        ];

        SeriesTicketLastAct.Of(acts, "43").Should().Be(new SeriesTicketLastAct("refreshed SCRUM-43", "it conflicts", SeriesRecords.T.AddMinutes(2)));
        SeriesTicketLastAct.Of(acts, "45").Should().BeNull();
    }

    [Fact]
    public async Task TheMastersSurface_IsAtMostTwentyTools()
    {
        var reads = new Mock<ISeriesRecordReads>();
        reads.Setup(r => r.Tools()).Returns([.. new[] { "read_run", "read_llm_calls", "read_run_events" }
            .Select(name => (AITool)AIFunctionFactory.Create(() => name, name))]);

        await JudgeAsync(null, records: reads.Object);

        _tools.Select(t => t.Name).Should().OnlyHaveUniqueItems().And.HaveCountLessThanOrEqualTo(20)
            .And.Contain(["merge_pull_request", "refresh_branch", "retarget_pull_request", "ci", "add_ticket", "link_tickets"])
            .And.NotContain("propose_recut", "2026-11-25-9992f: the master re-cuts the series itself");
    }
}
