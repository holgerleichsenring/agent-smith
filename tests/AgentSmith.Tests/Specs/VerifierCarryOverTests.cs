using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Events;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-11-18-d6d2c: the verifier keeps its word. Every gap of a criterion in one pass, the honest cost of a "not
/// satisfied", the spec's record beside the diff, and — after a repair — the previous findings with the files the repair
/// changed, so a gap it missed before on code the repair left alone is advice on a satisfied row, not a stop.
/// </summary>
public sealed class VerifierCarryOverTests
{
    private const string Criterion = "only an eligible recipient can act";
    private const string File = "src/Api/Approvals/Eligibility.cs";
    private const string Diff = $"""
        diff --git a/{File} b/{File}
        --- a/{File}
        +++ b/{File}
        @@ -1,1 +1,2 @@
        +    public bool CanAct(Recipient r) => r.IsEligible;
        """;

    [Fact]
    public void Prompt_AsksForEveryGapAndStatesTheCost_WithoutTheCostsNothingLine()
    {
        var prompt = SpecAccountPrompt.For([Criterion], Diff, []);

        prompt.Should().Contain("name EVERY gap of a criterion in this one").And.Contain("A \"not satisfied\" stops the run")
            .And.Contain("not that every permutation has its own test").And.Contain("\"advice\"")
            .And.NotContain("costs you nothing");
        prompt.Should().NotContain("THE PREVIOUS VERIFICATION", "a first verification has nothing to carry over");
    }

    [Fact]
    public void Prompt_AfterARepair_CarriesThePreviousFindingsAndTheFilesTheRepairChanged()
    {
        var record = new AccountRecord("spec: s1\nsteps:\n  - id: a", ["repo: " + Criterion + " (expiry not rejected)"], ["repo: " + File]);

        var prompt = SpecAccountPrompt.For([Criterion], Diff, [], record: record);

        prompt.Should().Contain("THE SPEC AS RATIFIED").And.Contain("steps:")
            .And.Contain("THE PREVIOUS VERIFICATION OF THIS SPEC").And.Contain("- repo: " + Criterion + " (expiry not rejected)")
            .And.Contain("CLOSED or still OPEN").And.Contain("missed before")
            .And.Contain("FILES THE REPAIR CHANGED").And.Contain("- repo: " + File);
    }

    [Fact]
    public void Record_ReadsTheSpec_AndCarriesFindingsOnlyAfterAnAccountingRepair()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.CurrentSpec, new SpecDraft("s1", "goal", "spec: s1", []));

        AccountRecord.For(pipeline, ["repo: x"]).Should().Be(AccountRecord.None with { SpecYaml = "spec: s1" },
            "no repair yet: the files changed mean nothing without a previous list");

        pipeline.Set(ContextKeys.OutstandingCriteria, new List<string> { "repo: c (gap)" });
        var after = AccountRecord.For(pipeline, ["repo: x"]);
        after.PreviousFindings.Should().Equal("repo: c (gap)");
        after.RepairChanged.Should().Equal("repo: x");

        pipeline.Set(ContextKeys.OutstandingCriteria, new List<string> { StageRepair.ItemPrefix + " repo build" });
        AccountRecord.For(pipeline, ["repo: x"]).IsAfterRepair.Should().BeFalse("a red build was handed back, not findings");
    }

    [Fact]
    public async Task Accountant_SatisfiedRowWithAdvice_KeepsTheAdvice_AndThePromptCarriesTheRecord()
    {
        var client = new ScriptedChatClient();
        client.EnqueueText($$"""[{"criterion":"{{Criterion}}","disposition":"satisfied","citations":["{{File}}"],"note":"previous gap closed","advice":"no test for an e-mail-only recipient"}]""");
        var record = new AccountRecord("spec: s1", ["repo: " + Criterion + " (expiry)"], ["repo: " + File]);

        var account = await Accountant(client).AccountAsync("repo", [Criterion], Diff, [], new AgentConfig(), null,
            PipelineCostTracker.GetOrCreate(new PipelineContext()), CancellationToken.None, record: record);

        account.Criteria.Should().ContainSingle().Which.Advice.Should().Be("no test for an e-mail-only recipient");
        account.Delivered.Should().BeTrue("advice stops nothing");
        client.Prompts.Single().Should().Contain("THE PREVIOUS VERIFICATION OF THIS SPEC");
        SpecAccountRenderer.ToMarkdown([account]).Should().Contain("(advice: no test for an e-mail-only recipient)");
    }

    [Fact]
    public async Task Accountant_AdviceOnARowThatCitesNothing_IsStillNotSatisfied()
    {
        var client = new ScriptedChatClient();
        client.EnqueueText($$"""[{"criterion":"{{Criterion}}","disposition":"satisfied","citations":[],"advice":"thin"}]""");
        client.EnqueueText($$"""[{"criterion":"{{Criterion}}","disposition":"satisfied","citations":[],"advice":"thin"}]""");

        var account = await Accountant(client).AccountAsync("repo", [Criterion], Diff, [], new AgentConfig(), null,
            PipelineCostTracker.GetOrCreate(new PipelineContext()), CancellationToken.None);

        account.Criteria.Should().ContainSingle().Which.IsOutstanding.Should().BeTrue("advice never stands in for a citation");
    }

    [Fact]
    public void AttackLens_OneDoneLineIsOneCheckableClaim() =>
        SpecAttackPrompt.DecidabilityLens.Should().Contain("One done line is one checkable claim")
            .And.Contain("one line per claim");

    private static SpecAccountant Accountant(IChatClient client)
    {
        var factory = new StubChatClientFactory(client);
        return new SpecAccountant(factory,
            new AccountCalls(new SpecAccountCall(factory, new AsyncLocalRunContextAccessor(), NullLogger<SpecAccountCall>.Instance)),
            NullLogger<SpecAccountant>.Instance);
    }

    private sealed class ScriptedChatClient : IChatClient
    {
        private readonly Queue<string> _answers = new();

        public List<string> Prompts { get; } = [];

        public void EnqueueText(string text) => _answers.Enqueue(text);

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            Prompts.Add(string.Join("\n", messages.Select(m => m.Text)));
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, _answers.Count > 0 ? _answers.Dequeue() : "[]")));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }

    private sealed class StubChatClientFactory(IChatClient client) : IChatClientFactory
    {
        public IChatClient Create(AgentConfig agent, TaskType task, int? maxIterations = null, MasterLoopHooks? masterLoopHooks = null) => client;

        public int GetMaxOutputTokens(AgentConfig agent, TaskType task) => 4096;

        public string GetModel(AgentConfig agent, TaskType task) => "stub-model";
    }
}
