using System.Text.Json;
using AgentSmith.Contracts.Models.Workers;
using AgentSmith.Infrastructure.Services.Workers;

namespace AgentSmith.Tests.TestHelpers;

/// <summary>
/// p0416: answers external-worker calls from a FIFO script instead of spawning a CLI.
/// Everything above the subprocess — request composition, prompt rendering, reply
/// parsing, translation back into a ChatResponse — is the SAME code a live CLI-driven
/// run uses; only the process is scripted, which is what makes the harness scenario
/// deterministic in CI while the bridge itself stays honest.
/// </summary>
public sealed class ScriptedWorkerProcessRunner : IWorkerProcessRunner
{
    private readonly WorkerJsonFormat _json = new();
    private readonly Queue<WorkerProcessResult> _results = new();

    /// <summary>Every prompt the bridge handed the worker, in order.</summary>
    public List<string> Prompts { get; } = [];

    public ScriptedWorkerProcessRunner EnqueueText(string text) =>
        EnqueueReply(new WorkerReply(Text: text));

    public ScriptedWorkerProcessRunner EnqueueToolCall(string name, string argumentsJson) =>
        EnqueueReply(new WorkerReply(ToolCalls:
            [new WorkerToolCall(name, JsonDocument.Parse(argumentsJson).RootElement)]));

    public ScriptedWorkerProcessRunner EnqueueReply(WorkerReply reply) =>
        EnqueueRaw(_json.Serialize(reply));

    public ScriptedWorkerProcessRunner EnqueueRaw(
        string stdout, int exitCode = 0, string stderr = "", bool timedOut = false)
    {
        _results.Enqueue(new WorkerProcessResult(
            exitCode, stdout, stderr, TimeSpan.FromMilliseconds(1), timedOut));
        return this;
    }

    public Task<WorkerProcessResult> RunAsync(
        string prompt, ExternalWorkerCliOptions options, CancellationToken cancellationToken)
    {
        Prompts.Add(prompt);
        return Task.FromResult(_results.Count > 0 ? _results.Dequeue() : DefaultEmpty());
    }

    // Mirrors ScriptedChatClient's benign default: an empty JSON object ends agentic
    // loops and parses as "nothing to do" for structured-output handlers.
    private WorkerProcessResult DefaultEmpty() =>
        new(0, _json.Serialize(new WorkerReply(Text: "{}")), string.Empty,
            TimeSpan.FromMilliseconds(1), false);
}
