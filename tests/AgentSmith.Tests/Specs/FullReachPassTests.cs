using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-08-25-6f12: a criterion no diff window satisfied is asked once more with the whole
/// branch in reach, on a search allowance of its own.
/// <para>
/// A live two-repository migration was refused on two ratified criteria while all four builds
/// and test runs in both repositories exited 0. One refusal read "the required branch-wide
/// absence search could not run" — the search had not failed, it had never run: twelve
/// searches were capped on the ACCOUNT and the windowed pass had spent all of them. The other
/// spoke of hosts in both repositories, judged inside a window that held one.
/// </para>
/// </summary>
public sealed class FullReachPassTests
{
    private const string Server = "Sample.Server";
    private const string ServerFile = "src/Messaging/ServerInstaller.cs";
    private const string WorkerFile = "src/Messaging/WorkerInstaller.cs";
    private const string BaseRef = "origin/main";

    /// <summary>A sentence the full-reach ask carries and no other prompt does.</summary>
    private const string FullReachMark = "with the WHOLE branch in reach";

    private const string Universal = "each applicable host declares the broker extension";
    private const string Other = "the shared name formatter names every exchange";
    private const string Conditional = "wherever the topic transport was already configured it is shortened";

    // ---------- the pass ----------

    /// <summary>The defect itself: two windows, a criterion whose subjects are split across
    /// them, and neither window able to say yes on its own.</summary>
    [Fact]
    public async Task Accountant_AUniversalCriterionSplitAcrossWindows_IsSettledByTheFullReachPass()
    {
        var run = await AccountAsync([Universal], (prompt, search) =>
            prompt.Contains(FullReachMark, StringComparison.Ordinal)
                ? Answer(Sat(Universal, ServerFile, WorkerFile))
                : Answer(No(Universal)));

        run.Client.Prompts.Should().HaveCountGreaterThan(2,
            "the delivery has to split for the windows to disagree with the branch");
        run.Account.Criteria.Should().ContainSingle()
            .Which.Disposition.Should().Be(AccountDisposition.Satisfied);
    }

    /// <summary>The mechanism the live run would have arrived to find inert. The windowed
    /// pass spends every search it may; the full-reach pass still gets to look.</summary>
    [Fact]
    public async Task Accountant_TheFullReachPass_HasSearchesLeftAfterAnExhaustedWindowedPass()
    {
        var lastAnswer = string.Empty;
        var run = await AccountAsync([Universal], async (prompt, search) =>
        {
            if (prompt.Contains(FullReachMark, StringComparison.Ordinal))
            {
                lastAnswer = await search!.SearchBranch(Server, "BrokerExtension");
                return Answer(No(Universal));
            }
            for (var i = 0; i < AccountSearchBudget.PerPass; i++)
                await search!.SearchBranch(Server, $"windowed{i}");
            return Answer(No(Universal));
        });

        lastAnswer.Should().NotContain("No search left",
            "an allowance the windows can empty is an allowance this pass never has");
        run.Sandbox.Ran.Should().HaveCount(AccountSearchBudget.PerPass + 1,
            "the windows spend their own allowance and this pass opens a fresh one");
    }

    /// <summary>One BranchSearch, so one resolver: a citation naming the WINDOWED pass's
    /// search and one naming this pass's own both resolve against the same evidence.</summary>
    [Fact]
    public async Task Accountant_TheFullReachPass_EvidenceResolvesThroughTheSameResolver()
    {
        var run = await AccountAsync([Universal], async (prompt, search) =>
        {
            if (!prompt.Contains(FullReachMark, StringComparison.Ordinal))
            {
                await search!.SearchBranch(Server, "WindowedPattern");
                return Answer(No(Universal));
            }
            await search!.SearchBranch(Server, "FullReachPattern");
            return Answer(Sat(Universal, "WindowedPattern", "FullReachPattern"));
        });

        var row = run.Account.Criteria.Should().ContainSingle().Subject;
        row.Disposition.Should().Be(AccountDisposition.Satisfied);
        row.Citation.Should().Contain("WindowedPattern").And.Contain("FullReachPattern",
            "both passes searched the same branch and one resolver reads what they found");
    }

    /// <summary>A criterion the account satisfied is an answer, and asking again would be
    /// asking for a change of mind.</summary>
    [Fact]
    public async Task Accountant_ACriterionSatisfiedByAWindow_IsNotReAsked()
    {
        var run = await AccountAsync([Universal, Other], (prompt, search) =>
            prompt.Contains(FullReachMark, StringComparison.Ordinal)
                ? Answer(No(Other))
                : Answer(Sat(Universal, ServerFile), No(Other)));

        var asked = run.Client.Prompts.Single(p => p.Contains(FullReachMark, StringComparison.Ordinal));
        asked.Should().Contain(Other).And.NotContain(Universal,
            "only what no window settled is put again");
    }

    [Fact]
    public async Task Accountant_TheFullReachPass_CanRaiseButNeverLowerADisposition()
    {
        var run = await AccountAsync([Universal, Conditional], async (prompt, search) =>
        {
            if (prompt.Contains(FullReachMark, StringComparison.Ordinal))
                return Answer(Sat(Universal, ServerFile), No(Conditional));
            await search!.SearchBase(Server, "TopicTransport");
            return Answer(
                No(Universal),
                Na(Conditional, "a previously configured topic transport", "TopicTransport"));
        });

        Of(run.Account, Universal).Disposition.Should().Be(AccountDisposition.Satisfied,
            "wider reach may raise what one slice could not show");
        Of(run.Account, Conditional).Disposition.Should().Be(AccountDisposition.NotApplicable,
            "a later answer merges by rank, so it can never take back what an earlier one settled");
    }

    /// <summary>Once. A second full-reach pass is a carousel with a search bill, and the
    /// account is the last thing a run does.</summary>
    [Fact]
    public async Task Accountant_TheFullReachPass_RunsAtMostOnce()
    {
        var run = await AccountAsync([Universal], (_, _) => Answer(No(Universal)));

        run.Client.Prompts.Count(p => p.Contains(FullReachMark, StringComparison.Ordinal))
            .Should().Be(1, "a pass that still finds nothing has answered, not failed");
        run.Account.Outstanding.Should().ContainSingle();
    }

    [Fact]
    public async Task Accountant_NoCriterionOutstanding_SkipsTheFullReachPass()
    {
        var run = await AccountAsync([Universal],
            (_, _) => Answer(Sat(Universal, ServerFile)));

        run.Client.Prompts.Should().NotContain(p => p.Contains(FullReachMark, StringComparison.Ordinal));
        run.Log.Lines.Should().Contain(l => l.Contains("no full-reach pass", StringComparison.Ordinal));
    }

    /// <summary>Its whole evidence is a search, so without a sandbox there is nothing for it
    /// to do — and it says which of the two reasons applied, because a silent skip and a pass
    /// that found nothing read identically afterwards.</summary>
    [Fact]
    public async Task Accountant_NoSandbox_SkipsTheFullReachPassAndSaysSo()
    {
        var run = await AccountAsync([Universal],
            (_, _) => Answer(No(Universal)), withSandbox: false);

        run.Client.Prompts.Should().NotContain(p => p.Contains(FullReachMark, StringComparison.Ordinal));
        run.Log.Lines.Should().Contain(l => l.Contains("no sandbox to search", StringComparison.Ordinal));
    }

    // ---------- the parts ----------

    /// <summary>The allowance is per PASS: opening one grants a fresh twelve whatever the
    /// passes before it spent, which is what "capacity the windows cannot consume" means.
    /// </summary>
    [Fact]
    public void SearchBudget_OpeningTheNextPass_GrantsAFreshAllowance()
    {
        var budget = new AccountSearchBudget();
        for (var i = 0; i < AccountSearchBudget.PerPass; i++) budget.TryTake().Should().BeTrue();
        budget.TryTake().Should().BeFalse("one pass may run twelve");

        budget.OpenNextPass();

        budget.TryTake().Should().BeTrue("the next pass does not inherit an empty purse");
    }

    [Fact]
    public void Unsettled_TakesEveryRowAWindowDidNotSatisfy()
    {
        IReadOnlyList<CriterionAccount> rows =
        [
            new("satisfied one", AccountDisposition.Satisfied),
            new("declined one", AccountDisposition.NotApplicable),
            new("refused one", AccountDisposition.NotSatisfied),
        ];

        AccountFullReachPass.Unsettled(rows).Select(r => r.Criterion)
            .Should().Equal("declined one", "refused one");
    }

    /// <summary>The ask has to reach every repository the criterion covers, and must not read
    /// as a plea — the one defect this pass could introduce is a criterion talked into
    /// passing on a second attempt.</summary>
    [Fact]
    public void FullReachAsk_NamesEveryRepositoryAndStillAsksForTheRefusal()
    {
        var message = AccountFullReachAsk.Message(
            [new CriterionAccount(Universal, AccountDisposition.NotSatisfied, null, "no window showed it")],
            [Server, "Sample.Worker"]);

        message.Should().Contain(Server).And.Contain("Sample.Worker");
        message.Should().Contain("report it not satisfied");
        message.Should().Contain("proves nothing about another");
    }

    // ---------- the rig ----------

    private static CriterionAccount Of(SpecAccount account, string criterion) =>
        account.Criteria.Single(c => string.Equals(c.Criterion, criterion, StringComparison.Ordinal));

    /// <summary>One JSON array, whatever the rows say — two arrays back to back is not an
    /// answer the reader can take, and a test that produced one would be measuring the
    /// reader.</summary>
    private static string Answer(params string[] rows) => "[" + string.Join(",", rows) + "]";

    private static string Sat(string criterion, params string[] citations) =>
        Row(criterion, "satisfied", citations, antecedent: null);

    private static string No(string criterion) =>
        Row(criterion, "not_satisfied", [], antecedent: null);

    private static string Na(string criterion, string antecedent, string citation) =>
        Row(criterion, "not_applicable", [citation], antecedent);

    private static string Row(
        string criterion, string disposition, IReadOnlyList<string> citations, string? antecedent)
    {
        var cited = string.Join(",", citations.Select(c => $"\"{c}\""));
        var extra = antecedent is null ? string.Empty : $",\"antecedent\":\"{antecedent}\"";
        return $"{{\"criterion\":\"{criterion}\",\"disposition\":\"{disposition}\","
            + $"\"citations\":[{cited}]{extra},\"note\":\"n\"}}";
    }

    /// <summary>Two files, each big enough that the declared budget cannot hold both — which
    /// is the whole point: a criterion over both is unanswerable from either window.</summary>
    private static string Delivery() =>
        FileDiff(ServerFile, "PublishMessage<OrderPlaced>().ToExchange")
        + FileDiff(WorkerFile, "ListenToQueue");

    private static string FileDiff(string path, string marker)
    {
        var body = string.Join("\n", Enumerable.Range(0, 40).Select(i => $"+    // line {i} {marker}"));
        return $"diff --git a/{path} b/{path}\n--- a/{path}\n+++ b/{path}\n@@ -0,0 +1,40 @@\n{body}\n";
    }

    private sealed record Run(
        SpecAccount Account, ScriptedAccount Client, CapturingLogger<SpecAccountant> Log,
        CountingSandbox Sandbox);

    private static Task<Run> AccountAsync(
        IReadOnlyList<string> criteria, Func<string, BranchSearch?, string> respond,
        bool withSandbox = true) =>
        AccountAsync(criteria, (prompt, search) => Task.FromResult(respond(prompt, search)), withSandbox);

    private static async Task<Run> AccountAsync(
        IReadOnlyList<string> criteria, Func<string, BranchSearch?, Task<string>> respond,
        bool withSandbox = true)
    {
        var sandbox = new CountingSandbox();
        BranchSearch? search = withSandbox
            ? new BranchSearch(
                new Dictionary<string, ISandbox> { [Server] = sandbox }, NullLogger.Instance,
                new Dictionary<string, string?> { [Server] = BaseRef })
            : null;
        var client = new ScriptedAccount(prompt => respond(prompt, search));
        var factory = new SingleClientFactory(client);
        var log = new CapturingLogger<SpecAccountant>();
        var accountant = new SpecAccountant(
            factory,
            new AccountCalls(new SpecAccountCall(
                factory, new NullRunContext(), NullLogger<SpecAccountCall>.Instance)),
            log);

        var account = await accountant.AccountAsync(
            Server, criteria, Delivery(), [], new AgentConfig(), search,
            new PipelineCostTracker(), CancellationToken.None, windowBudgetChars: 900);
        return new Run(account, client, log, sandbox);
    }

    /// <summary>Grep exiting 1 is grep finding nothing, which is what an absence proof and a
    /// disproved antecedent both rest on.</summary>
    private sealed class CountingSandbox : ISandbox
    {
        public string JobId => "full-reach";
        public List<Step> Ran { get; } = [];

        public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? progress, CancellationToken ct)
        {
            Ran.Add(step);
            return Task.FromResult(new StepResult(
                StepResult.CurrentSchemaVersion, step.StepId, 1, false, 0.1, null, string.Empty));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>Stands in for the model: it reads the prompt, may search the way a model
    /// would, and answers.</summary>
    private sealed class ScriptedAccount(Func<string, Task<string>> respond) : IChatClient
    {
        public List<string> Prompts { get; } = [];

        public async Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken ct = default)
        {
            var prompt = string.Join("\n", messages.Select(m => m.Text));
            Prompts.Add(prompt);
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, await respond(prompt)));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken ct = default) => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }

    private sealed class SingleClientFactory(IChatClient client) : IChatClientFactory
    {
        public IChatClient Create(
            AgentConfig agent, TaskType task, int? maxIterations = null,
            MasterLoopHooks? masterLoopHooks = null) => client;

        public int GetMaxOutputTokens(AgentConfig agent, TaskType task) => 4096;

        public string GetModel(AgentConfig agent, TaskType task) => "scripted";
    }

    private sealed class NullRunContext : AgentSmith.Contracts.Events.IRunContextAccessor
    {
        public string? CurrentRunId => null;
        public AgentSmith.Contracts.Events.CallScope? CurrentCallScope => null;
        public IDisposable BeginScope(string runId) => new Scope();
        public int? CurrentStepIndex => null;
        public string? CurrentPhaseId => null;
        public IDisposable BeginStepScope(int stepIndex, string? phaseId = null) => new Scope();
        public IDisposable BeginCallScope(string role, string phase, string? repoName = null) => new Scope();

        private sealed class Scope : IDisposable { public void Dispose() { } }
    }
}
