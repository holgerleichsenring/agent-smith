using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Events;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using static AgentSmith.Tests.Specs.CutReviewTestDoubles;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-10-13-a347b: an outcome the ticket lists under its own "Done when" that no done entry
/// states is a finding that quotes the TICKET — admitted when the ticket contains the quote,
/// refused when a spec already states it as a done entry.
/// </summary>
public sealed class CutReviewDoneDroppedTests
{
    private const string Ticket = """
        Guard the export against an empty batch.

        Done when:
        - the tests pass
        - a test proves the change
        - a test proves the guard
        """;

    private const string Kept = "the tests pass";
    private const string Dropped = "a test proves the guard";

    [Fact]
    public async Task SpecCutAdmission_DoneDroppedQuotingTheTicket_IsAdmitted()
    {
        var review = await Review([Draft(Kept)], DoneDropped("p1a", Dropped));

        review.Findings.Should().ContainSingle().Which.Problem.Should().Be(SpecCutVerdicts.DoneDropped);
    }

    [Fact]
    public async Task SpecCutAdmission_DoneDroppedQuotingADoneEntry_IsRefused()
    {
        var other = Draft(Dropped) with { SpecId = "p1b" };

        var review = await Review([Draft(Kept), other], DoneDropped("p1a", Dropped));

        review.Deliverable.Should().BeTrue("an outcome some spec states as a done entry was not dropped");
    }

    [Theory]
    [InlineData("a test proves the export is fast")]
    [InlineData("guard")]
    public async Task SpecCutAdmission_DoneDroppedNotInTheTicket_IsRefused(string quote)
    {
        var review = await Review([Draft(Kept)], DoneDropped("p1a", quote));

        review.Deliverable.Should().BeTrue("a quote the ticket does not carry, or one word of it, identifies nothing");
    }

    [Fact]
    public async Task Derivation_AFirstCutDroppingADoneWhenOutcome_IsRejectedAndRederived()
    {
        var deriverChat = new Scripted(Cut(Kept), Cut(Kept, Dropped));
        var reviewerChat = new Scripted(DoneDropped("export-guard", Dropped), "[]");
        var deriver = new SpecSetDeriver(
            AgentSmith.Tests.TestSupport.CutReviewSteps.Of(Reviewer(new FixedFactory(reviewerChat))),
            new SpecDerivationCall(new FixedFactory(deriverChat), new AsyncLocalRunContextAccessor(),
                DerivationTestLooks.Proof()),
            DerivationTestLooks.Factory(), new FixedPrompt(), DerivationTestParsers.Real(),
            new ScopedContextCoverage(), AgentSmith.Tests.TestSupport.HandbackTestDoubles.Gate(),
            AgentSmith.Tests.TestSupport.SeriesSettingsDoubles.Reader(), NullLogger<SpecSetDeriver>.Instance);

        var (derivation, error) = await deriver.DeriveAsync(
            new Ticket(new TicketId("1"), "guard", Ticket, null, "open", "test"),
            TicketSegmenter.Segment(Ticket), previous: null, series: "2026-10-13-0a0a", cause: "initial derivation",
            repositories: [], new AgentConfig(), new PipelineContext(), CancellationToken.None);

        error.Should().BeNull();
        reviewerChat.Prompts.Should().HaveCount(2, "the first cut was reviewed, rejected and the second reviewed");
        deriverChat.Prompts[1].Should().Contain($"\"{Dropped}\" — done-dropped");
        derivation!.Set.Specs.Should().ContainSingle().Which.Draft.Done.Should().Contain(Dropped);
    }

    private static async Task<SpecCutReview> Review(IReadOnlyList<SpecDraft> drafts, string answer) =>
        await Reviewer(new FixedFactory(new Scripted(answer))).ReviewAsync(
            drafts, Key, Ticket, look: null, new AgentConfig(), Tracker(), CancellationToken.None);

    private static string DoneDropped(string spec, string quote) =>
        $$"""[{"spec_id":"{{spec}}","criterion":"{{quote}}","problem":"done-dropped","why":"the ticket lists it as done"}]""";

    private static SpecDraft Draft(params string[] done) =>
        new("p1a", "guard the export", "spec: p1a", []) { Done = done };

    private static string Cut(params string[] done)
    {
        var carries = string.Join(",", TicketSegmenter.Segment(Ticket).Select(s => s.Id));
        var entries = string.Join(",", done.Select(d => $"\"{d}\""));
        return $$$"""
            {"specs": [
               {"slug": "export-guard", "goal": "Guard the export against an empty batch",
                "steps": [{"id": "guard", "action": "Add the guard"}],
                "done": [{{{entries}}}], "carries": [{{{carries}}}]}],
             "discarded": [], "ignored_instructions": [],
             "handback": {"case": "none", "reason": ""}}
            """;
    }

    /// <summary>Answers in the order scripted, and keeps the last message of every request.</summary>
    private sealed class Scripted(params string[] answers) : IChatClient
    {
        private int _turn;

        public List<string> Prompts { get; } = [];

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken ct = default)
        {
            Prompts.Add(messages.Last().Text);
            var answer = answers[Math.Min(_turn++, answers.Length - 1)];
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, answer)));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken ct = default) => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }

    private sealed class FixedFactory(IChatClient client) : IChatClientFactory
    {
        public IChatClient Create(
            AgentConfig agent, TaskType task, int? maxIterations = null, MasterLoopHooks? masterLoopHooks = null)
            => client;

        public int GetMaxOutputTokens(AgentConfig agent, TaskType task) => 4096;

        public string GetModel(AgentConfig agent, TaskType task) => "stub-model";
    }

    private sealed class FixedPrompt : IPromptCatalog
    {
        public string Get(string name) => "cut the ticket";

        public string Render(string name, IReadOnlyDictionary<string, string> tokens) => Get(name);
    }
}
