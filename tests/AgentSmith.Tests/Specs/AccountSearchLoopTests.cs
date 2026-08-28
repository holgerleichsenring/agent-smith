using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// p0483: the tool is not the phase — the LOOP is. Asserting that search_branch reaches
/// ChatOptions proves it was offered; this proves that a model which CALLS it gets an
/// answer back and can decide on it. Without this the phase could ship inert, and the only
/// place that would show is a forty minute live run.
/// </summary>
public sealed class AccountSearchLoopTests
{
    private const string Repo = "Sample.Server";

    private sealed class CountingSandbox(int exitCode) : ISandbox
    {
        public string JobId => "loop";
        public List<Step> Ran { get; } = [];

        public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? progress, CancellationToken ct)
        {
            Ran.Add(step);
            return Task.FromResult(new StepResult(
                StepResult.CurrentSchemaVersion, step.StepId, exitCode, false, 0.1, null, string.Empty));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>Answers a tool call first, then the account. The same shape the provider
    /// produces, so the invocation middleware is what is under test.</summary>
    private sealed class ToolCallingChatClient(string arguments) : IChatClient
    {
        private int _turn;

        public List<ChatMessage> LastMessages { get; private set; } = [];

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken ct = default)
        {
            LastMessages = [.. messages];
            if (_turn++ == 0)
                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant,
                [
                    new FunctionCallContent("call-1", "search_branch",
                        new Dictionary<string, object?>
                        {
                            ["repository"] = Repo,
                            ["pattern"] = "MassTransit",
                            ["arguments"] = arguments,
                        }),
                ])));
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant,
                """[{"criterion":"no MassTransit remains","satisfied":true,"citations":["searched"],"note":"searched"}]""")));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken ct = default) => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }

    /// <summary>
    /// The whole chain in one: the account is offered the tool, calls it, the search runs
    /// against the sandbox, and its answer comes back as a tool result the next turn reads.
    /// </summary>
    [Fact]
    public async Task AnAccountThatCallsTheSearch_RunsItAgainstTheSandbox_AndReadsTheAnswer()
    {
        var sandbox = new CountingSandbox(exitCode: 1);
        var search = new BranchSearch(
            new Dictionary<string, ISandbox> { [Repo] = sandbox }, NullLogger.Instance);
        var provider = new ToolCallingChatClient("unused");
        using var chat = provider.AsBuilder().UseFunctionInvocation().Build();

        var response = await chat.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "account for it")],
            new ChatOptions { Tools = AccountTools.For(search) },
            CancellationToken.None);

        sandbox.Ran.Should().ContainSingle("the model's call reached the sandbox")
            .Which.Command.Should().Be("grep");
        // A tool result travels as FunctionResultContent, never as message text — reading
        // .Text here would pass on an empty result and prove nothing.
        provider.LastMessages
            .SelectMany(m => m.Contents).OfType<FunctionResultContent>()
            .Select(r => r.Result?.ToString() ?? string.Empty)
            .Should().ContainSingle(t => t.Contains("does not occur anywhere", StringComparison.Ordinal),
                "the search's answer comes back as a tool result the account can decide on");
        response.Text.Should().Contain("no MassTransit remains");
    }

    /// <summary>The budget is a real fence, not a comment: the loop stops handing out
    /// searches and the account is told to decide on what it has.</summary>
    [Fact]
    public async Task TheSearchBudget_BindsInsideTheLoop()
    {
        var sandbox = new CountingSandbox(exitCode: 1);
        var search = new BranchSearch(
            new Dictionary<string, ISandbox> { [Repo] = sandbox }, NullLogger.Instance);

        for (var i = 0; i <= AccountSearchBudget.PerPass + 3; i++)
            await search.SearchBranch(Repo, $"pattern{i}");

        sandbox.Ran.Should().HaveCount(AccountSearchBudget.PerPass);
    }
}
