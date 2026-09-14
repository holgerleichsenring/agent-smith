using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// p0439: the state a phase verified is named the moment it is verified — the commit,
/// provided the source tree is clean. A dirty source tree names nothing.
/// </summary>
public sealed class VerifiedHeadsTests
{
    private readonly VerifiedHeads _sut = new(NullLogger<VerifiedHeads>.Instance);

    [Fact]
    public async Task Record_ACleanTree_NamesTheHead()
    {
        var pipeline = new PipelineContext();
        var sandbox = new GitAnsweringSandbox(status: "", head: "abc123\n");

        await _sut.RecordAsync(pipeline, Sandboxes(sandbox), CancellationToken.None);

        VerifiedHeads.For(pipeline, "server").Should().Be("abc123");
    }

    [Fact]
    public async Task Record_UncommittedSource_NamesNoHead()
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyDictionary<string, string>>(
            ContextKeys.VerifiedHeads, new Dictionary<string, string> { ["server"] = "older" });
        var sandbox = new GitAnsweringSandbox(status: " M src/Guard.cs\n", head: "abc123");

        await _sut.RecordAsync(pipeline, Sandboxes(sandbox), CancellationToken.None);

        VerifiedHeads.For(pipeline, "server").Should().BeNull(
            "the verified work is not on any commit, so no state can be delivered from it");
        sandbox.Asked.Should().NotContain(a => a.Contains("rev-parse"));
    }

    [Fact]
    public async Task Record_OnlyTheRunRecordUncommitted_StillNamesTheHead()
    {
        var pipeline = new PipelineContext();
        var sandbox = new GitAnsweringSandbox(
            status: "?? .agentsmith/runs/r1/plan.md\nR  .agentsmith/a.md -> .agentsmith/b.md\n", head: "abc123");

        await _sut.RecordAsync(pipeline, Sandboxes(sandbox), CancellationToken.None);

        VerifiedHeads.For(pipeline, "server").Should().Be("abc123", "the run record is not source");
    }

    [Fact]
    public async Task Record_AHeadGitCannotName_RecordsNothing()
    {
        var pipeline = new PipelineContext();
        var sandbox = new GitAnsweringSandbox(status: "", head: "");

        await _sut.RecordAsync(pipeline, Sandboxes(sandbox), CancellationToken.None);

        VerifiedHeads.For(pipeline, "server").Should().BeNull();
    }

    [Fact]
    public void For_NothingRecorded_IsNull()
    {
        VerifiedHeads.For(new PipelineContext(), "server").Should().BeNull();
    }

    private static IReadOnlyDictionary<string, ISandbox> Sandboxes(ISandbox sandbox) =>
        new Dictionary<string, ISandbox> { ["server"] = sandbox };

    private sealed class GitAnsweringSandbox(string status, string head) : ISandbox
    {
        public List<string> Asked { get; } = [];

        public string JobId => "git-answering";

        public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? progress, CancellationToken ct)
        {
            var args = string.Join(' ', step.Args ?? []);
            Asked.Add(args);
            var output = args.Contains("status") ? status : args.Contains("rev-parse") ? head : string.Empty;
            return Task.FromResult(new StepResult(
                StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0.01, null, output));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
