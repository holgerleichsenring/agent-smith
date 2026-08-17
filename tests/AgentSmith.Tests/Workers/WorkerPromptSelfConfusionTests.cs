using AgentSmith.Contracts.Models.Workers;
using AgentSmith.Infrastructure.Services.Workers;
using FluentAssertions;

namespace AgentSmith.Tests.Workers;

/// <summary>
/// p0424: the worker answers as the MODEL inside the loop, never as itself.
/// <para>
/// Measured in run 25: seven of twenty-seven answers carried no envelope, two of them
/// asking the operator to "grant Claude access" to the repository paths so it could
/// "execute all the changes directly". The answering model had slipped into being the
/// agent — and every such answer is a round in which nothing happens, which is what a
/// phase reading for four hours without a single write looks like from outside.
/// </para>
/// </summary>
public sealed class WorkerPromptSelfConfusionTests
{
    [Fact]
    public void ThePrompt_TellsTheWorkerItHasNoFilesystemOfItsOwn()
    {
        var prompt = new WorkerPromptRenderer(new WorkerJsonFormat()).Render(Request());

        prompt.Should().Contain("NO TOOLS AND NO FILESYSTEM");
        prompt.Should().Contain("not on any disk you can reach");
        prompt.Should().Contain("You are not being asked for permission",
            "the observed failure was the model asking for file permissions it never needed");
    }

    [Fact]
    public void ThePrompt_SaysHowToGetAFileInstead()
    {
        var prompt = new WorkerPromptRenderer(new WorkerJsonFormat()).Render(Request());

        prompt.Should().Contain("ask for the read as a tool call",
            "a prohibition without the alternative leaves the model with nowhere to go");
    }

    private static WorkerRequest Request() =>
        new("agentsmith.worker/1", "req-1", "run-1", 1, "coding-master", "Implementation",
            "primary", "external_worker", "sonnet", DateTimeOffset.UnixEpoch,
            [], [], new WorkerRequestOptions());
}
