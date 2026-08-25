using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-08-25-1360: every window is shown the file list of the whole DELIVERY.
/// <para>
/// The list used to be derived from the prompt's diff argument, and the accountant passes
/// one WINDOW as that argument. So an oversized delivery showed each window a partial list
/// under the heading "EVERY FILE THIS BRANCH CHANGED (complete, never truncated)", then told
/// it that a criterion it cannot tie to a file is not satisfied. It was instructed to refuse
/// a criterion over files it had been told did not exist.
/// </para>
/// </summary>
public sealed class DeliveryFileListTests
{
    private const string ServerFile = "src/Messaging/ServerInstaller.cs";
    private const string WorkerFile = "src/Messaging/WorkerInstaller.cs";

    /// <summary>Two files, each big enough that a small budget cannot hold both.</summary>
    private static string Delivery() =>
        FileDiff(ServerFile, "PublishMessage<OrderPlaced>().ToExchange", 40)
        + FileDiff(WorkerFile, "ListenToQueue", 40);

    private static string FileDiff(string path, string marker, int lines)
    {
        var body = string.Join("\n", Enumerable.Range(0, lines).Select(i => $"+    // line {i} {marker}"));
        return $"diff --git a/{path} b/{path}\n--- a/{path}\n+++ b/{path}\n@@ -0,0 +1,{lines} @@\n{body}\n";
    }

    [Fact]
    public void AccountPrompt_TheFileList_CoversTheWholeDeliveryNotTheBody()
    {
        var delivery = Delivery();
        var window = DiffWindows.Split(delivery, 600)[0];

        var prompt = SpecAccountPrompt.For(
            ["a criterion"], window, [], null, CitedFileIndex.FromDiff(delivery));

        prompt.Should().Contain(ServerFile).And.Contain(WorkerFile,
            "the heading calls the list complete, so it has to be");
    }

    /// <summary>Back-compatible by construction: a caller that names no delivery gets the
    /// list the body implies, which is what every non-windowed caller already relied on.</summary>
    [Fact]
    public void AccountPrompt_ASingleWindowDelivery_IsUnchanged()
    {
        var delivery = Delivery();

        SpecAccountPrompt.For(["a criterion"], delivery, [])
            .Should().Be(SpecAccountPrompt.For(
                ["a criterion"], delivery, [], null, CitedFileIndex.FromDiff(delivery)));
    }

    /// <summary>The other half of the trade. A complete list over a partial body widens what
    /// a window may close on a name it never read, so the prompt says what a name proves.</summary>
    [Fact]
    public void AccountPrompt_AListedFileAbsentFromTheBody_IsNotEvidenceOfItsContent()
    {
        var prompt = SpecAccountPrompt.For(["a criterion"], Delivery(), []);

        prompt.Should().Contain("proves that the file CHANGED")
            .And.Contain("settled from a body that shows it");
    }

    [Fact]
    public async Task Accountant_EveryWindow_IsShownTheSameCompleteFileList()
    {
        var recorder = await AccountAsync(Delivery(), windowBudget: 600);

        recorder.Prompts.Should().HaveCountGreaterThan(1, "the delivery has to split for this to mean anything");
        recorder.Prompts.Should().OnlyContain(p => p.Contains(ServerFile) && p.Contains(WorkerFile));
    }

    [Fact]
    public async Task Accountant_TheWindowBody_RemainsItsOwnSlice()
    {
        var recorder = await AccountAsync(Delivery(), windowBudget: 600);

        var bodies = recorder.Prompts.Select(Body).ToList();
        bodies.Distinct(StringComparer.Ordinal).Should().HaveCount(bodies.Count,
            "splitting is what keeps a delivery inside one call; only the LIST is shared");
        bodies.Should().Contain(b => b.Contains("ToExchange", StringComparison.Ordinal));
        bodies.Should().Contain(b => b.Contains("ListenToQueue", StringComparison.Ordinal));
    }

    /// <summary>
    /// The correction demands a path copied exactly as the FILE LIST prints it. Shown the
    /// first window's list, a criterion whose file lives in a later window was being asked to
    /// comply with a list that cannot contain it.
    /// </summary>
    [Fact]
    public async Task Accountant_TheCorrectionPass_IsShownTheWholeDeliverysFileList()
    {
        var recorder = await AccountAsync(
            Delivery(), windowBudget: 600,
            answer: """[{"criterion":"a criterion","satisfied":true,"citations":["nowhere/at/all.cs"],"note":"n"}]""");

        recorder.Prompts.Should().HaveCountGreaterThan(2, "an unresolvable citation is asked about once more");
        recorder.Prompts[^1].Should().Contain("resolves against nothing")
            .And.Contain(ServerFile).And.Contain(WorkerFile);
    }

    private static string Body(string prompt) => prompt[prompt.LastIndexOf("DIFF", StringComparison.Ordinal)..];

    private static async Task<RecordingChatClient> AccountAsync(
        string delivery, int windowBudget, string? answer = null)
    {
        var client = new RecordingChatClient(answer
            ?? """[{"criterion":"a criterion","satisfied":false,"citations":[],"note":"n"}]""");
        var factory = new SingleClientFactory(client);
        var accountant = new SpecAccountant(
            factory,
            new AccountCalls(new SpecAccountCall(factory, new NullRunContext(), NullLogger<SpecAccountCall>.Instance)),
            NullLogger<SpecAccountant>.Instance);

        await accountant.AccountAsync(
            "Sample.Server", ["a criterion"], delivery, [], new AgentConfig(),
            branchSearch: null, new PipelineCostTracker(), CancellationToken.None, windowBudget);
        return client;
    }

    private sealed class RecordingChatClient(string answer) : IChatClient
    {
        public List<string> Prompts { get; } = [];

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken ct = default)
        {
            Prompts.Add(string.Join("\n", messages.Select(m => m.Text)));
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, answer)));
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

        public string GetModel(AgentConfig agent, TaskType task) => "recording";
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
