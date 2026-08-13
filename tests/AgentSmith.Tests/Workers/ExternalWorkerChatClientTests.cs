using AgentSmith.Application.Services.Events;
using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Services.Workers;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Workers;

/// <summary>
/// p0416: the bridge as the agent loop sees it. A worker's tool call must reach the loop
/// as a real invocation, and every way a worker can fail must surface as a named failure
/// naming the run and the step — never as an empty response the loop then reasons about.
/// </summary>
public sealed class ExternalWorkerChatClientTests
{
    private static readonly ExternalWorkerCliOptions CliOptions =
        new("claude", ["-p"], TimeSpan.FromMinutes(5), "/tmp");

    private static (ExternalWorkerChatClient Client, IRunContextAccessor Context)
        NewClient(IWorkerProcessRunner runner)
    {
        var json = new WorkerJsonFormat();
        var context = new AsyncLocalRunContextAccessor();
        var client = new ExternalWorkerChatClient(
            new WorkerRequestComposer(new WorkerMessageMapper(json), new WorkerOptionsMapper()),
            new WorkerPromptRenderer(json),
            new WorkerReplyParser(json),
            new WorkerReplyTranslator(),
            runner, context, CliOptions, "external_worker", "sonnet",
            NullLogger.Instance);
        return (client, context);
    }

    private static async Task<T> InRunScope<T>(IRunContextAccessor context, Func<Task<T>> body)
    {
        using var run = context.BeginScope("run-42");
        using var step = context.BeginStepScope(7);
        using var call = context.BeginCallScope("coding-master", "Implementation", "primary");
        return await body();
    }

    [Fact]
    public async Task Bridge_ReplyWithToolCalls_ReachesTheLoopUnchanged()
    {
        var invokedWith = new List<string>();
        var tool = AIFunctionFactory.Create(
            (string path) => { invokedWith.Add(path); return "written"; }, "write_file", "Writes a file");
        var runner = new ScriptedWorkerProcessRunner()
            .EnqueueToolCall("write_file", """{"path":"src/Patch.cs"}""")
            .EnqueueText("done");
        var (client, context) = NewClient(runner);
        // The same function-invoking wrap production's ChatClientFactory puts above every
        // tool-bearing call: the worker's answer must drive a REAL tool invocation.
        var loop = new ChatClientBuilder(client).UseFunctionInvocation().Build();

        var response = await InRunScope(context, () => loop.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "fix it")],
            new ChatOptions { Tools = [tool] }, CancellationToken.None));

        invokedWith.Should().ContainSingle(
            "the worker's tool call must execute the framework's tool, not merely be echoed")
            .Which.Should().Be("src/Patch.cs");
        response.Text.Should().Contain("done");
        runner.Prompts.Should().HaveCount(2);
        runner.Prompts[1].Should().Contain("written",
            "the second call must show the worker the tool result, exactly as a provider would see it");
    }

    [Fact]
    public async Task Bridge_NonZeroExit_ThrowsNamingTheRunAndStep()
    {
        var runner = new ScriptedWorkerProcessRunner()
            .EnqueueRaw(string.Empty, exitCode: 1, stderr: "not logged in");
        var (client, context) = NewClient(runner);

        var act = async () => await InRunScope(context, () => client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "hi")], null, CancellationToken.None));

        (await act.Should().ThrowAsync<ExternalWorkerCallException>()).Which.Message
            .Should().Contain("run=run-42").And.Contain("step=7").And.Contain("not logged in");
    }

    [Fact]
    public async Task Bridge_Timeout_NamesTheRequestThatWentUnanswered()
    {
        var runner = new ScriptedWorkerProcessRunner()
            .EnqueueRaw(string.Empty, exitCode: -1, timedOut: true);
        var (client, context) = NewClient(runner);

        var act = async () => await InRunScope(context, () => client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "hi")], null, CancellationToken.None));

        (await act.Should().ThrowAsync<ExternalWorkerCallException>()).Which.Message
            .Should().Contain("did not answer within").And.Contain("role=coding-master");
    }

    [Fact]
    public async Task Bridge_UnparseableAnswer_ThrowsInsteadOfBecomingAssistantText()
    {
        var runner = new ScriptedWorkerProcessRunner().EnqueueRaw("I'll get right on it.");
        var (client, context) = NewClient(runner);

        var act = async () => await InRunScope(context, () => client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "hi")], null, CancellationToken.None));

        (await act.Should().ThrowAsync<ExternalWorkerCallException>()).Which.Reason
            .Should().Contain("not the agreed JSON object");
    }

    [Fact]
    public async Task Bridge_InventedTool_ThrowsRatherThanReachingTheLoop()
    {
        var tool = AIFunctionFactory.Create((string path) => "ok", "write_file", "Writes a file");
        var runner = new ScriptedWorkerProcessRunner().EnqueueToolCall("delete_repo", "{}");
        var (client, context) = NewClient(runner);

        var act = async () => await InRunScope(context, () => client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "hi")],
            new ChatOptions { Tools = [tool] }, CancellationToken.None));

        (await act.Should().ThrowAsync<ExternalWorkerCallException>()).Which.Reason
            .Should().Contain("delete_repo");
    }

    [Fact]
    public async Task Bridge_Streaming_IsRefusedRatherThanFaked()
    {
        var (client, _) = NewClient(new ScriptedWorkerProcessRunner());

        var act = () => client.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "hi")]);

        act.Should().Throw<NotSupportedException>();
        await Task.CompletedTask;
    }
}
