using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Loop;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Loop;

public sealed class TracingAIFunctionTests
{
    private static AIFunction MakeReadFile(Func<string, string>? body = null) =>
        AIFunctionFactory.Create(
            (string path) => body?.Invoke(path) ?? "file-contents",
            name: "read_file",
            description: "Read a file at the given path.");

    private static AIFunction MakeGrep() =>
        AIFunctionFactory.Create(
            (string pattern, string path) => $"matches for {pattern} in {path}",
            name: "grep",
            description: "Grep for a pattern.");

    private static AIFunction MakeFailing()
    {
        static string Body(string path) => throw new InvalidOperationException("boom");
        return AIFunctionFactory.Create(Body, name: "read_file");
    }

    [Fact]
    public async Task InvokeAsync_RecordsToolEntryOnSuccess()
    {
        var trace = new LoopTraceCollector();
        var wrapped = new TracingAIFunction(MakeReadFile(), trace);

        await wrapped.InvokeAsync(new AIFunctionArguments { ["path"] = "src/x.cs" });

        var entries = trace.Build();
        entries.Should().HaveCount(1);
        entries[0].Kind.Should().Be(LoopTraceEntryKind.ToolCall);
        entries[0].ToolName.Should().Be("read_file");
        entries[0].Success.Should().Be(true);
    }

    [Fact]
    public async Task InvokeAsync_ReadFile_RecordsPathInReadSet()
    {
        var trace = new LoopTraceCollector();
        var wrapped = new TracingAIFunction(MakeReadFile(), trace);

        await wrapped.InvokeAsync(new AIFunctionArguments { ["path"] = "src/Program.cs" });

        trace.ReadSet.Should().Contain("src/Program.cs");
    }

    [Fact]
    public async Task InvokeAsync_NonReadTool_DoesNotPopulateReadSet()
    {
        var trace = new LoopTraceCollector();
        var wrapped = new TracingAIFunction(MakeGrep(), trace);

        await wrapped.InvokeAsync(new AIFunctionArguments
        {
            ["pattern"] = "TODO",
            ["path"] = "src/"
        });

        trace.ReadSet.Should().BeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_InnerThrows_RecordsFailureAndRethrows()
    {
        var trace = new LoopTraceCollector();
        var wrapped = new TracingAIFunction(MakeFailing(), trace);

        Func<Task> act = async () => { await wrapped.InvokeAsync(new AIFunctionArguments { ["path"] = "src/x.cs" }); };

        await act.Should().ThrowAsync<InvalidOperationException>();
        var entries = trace.Build();
        entries.Should().HaveCount(1);
        entries[0].Success.Should().Be(false);
        entries[0].ErrorMessage.Should().Contain("boom");
    }

    [Fact]
    public async Task InvokeAsync_DelegatesNameAndDescriptionFromInner()
    {
        var trace = new LoopTraceCollector();
        var inner = MakeReadFile();
        var wrapped = new TracingAIFunction(inner, trace);

        wrapped.Name.Should().Be(inner.Name);
        wrapped.Description.Should().Be(inner.Description);
        await Task.CompletedTask;
    }
}
