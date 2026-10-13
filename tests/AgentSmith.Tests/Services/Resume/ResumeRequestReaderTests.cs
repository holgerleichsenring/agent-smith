using System.Text.Json;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Resume;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Services.SpecRename;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services.Resume;

/// <summary>
/// 2026-10-12-967ah: no reader maps an old command name or context type any more, so a checkpoint
/// that still carries one — written before the store rewrite reached it — is refused before any
/// step runs, naming what it cannot resolve. An in-flight run is disposable; a half-resumed one is not.
/// </summary>
public sealed class ResumeRequestReaderTests
{
    private readonly PipelineContextSerializer _serializer = new(NullLogger<PipelineContextSerializer>.Instance);
    private readonly Mock<ICommandContextFactory> _commands = new();

    public ResumeRequestReaderTests() =>
        _commands.Setup(f => f.Knows(It.IsAny<string>()))
            .Returns<string>(name => !SpecRenameTable.CommandNames.Any(r => r.Old == name));

    [Fact]
    public void ResumeRequestReader_UnresolvableOldCommand_FailsBeforeAnyStep()
    {
        var old = SpecRenameTable.CommandNames.Single(r => r.New == CommandNames.VerifySpec).Old;
        var context = new Mock<IPipelineContextSerializer>();
        context.Setup(s => s.Unresolvable(It.IsAny<string>())).Returns([]);
        var pipeline = Resuming([new CheckpointCommand(old), new CheckpointCommand(CommandNames.AgenticMaster)], "[]");

        var act = () => Reader(context.Object).TryRead(pipeline);

        act.Should().Throw<InvalidOperationException>().WithMessage($"*unknown command '{old}'*");
        context.Verify(s => s.Restore(It.IsAny<string>(), It.IsAny<PipelineContext>()), Times.Never,
            "nothing of the checkpoint is applied when it cannot be resumed whole");
    }

    [Fact]
    public void ResumeRequestReader_UnresolvableContextType_IsNamed()
    {
        var old = SpecRenameTable.TypeNames[0].Old + ", AgentSmith.Contracts";
        var entries = JsonSerializer.Serialize(new[] { new { K = ContextKeys.CurrentSpec, T = old, V = "{}" } });
        var pipeline = Resuming([new CheckpointCommand(CommandNames.AgenticMaster)], entries);

        var act = () => Reader(_serializer).TryRead(pipeline);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*unknown context entry type {ContextKeys.CurrentSpec} ({old})*");
    }

    [Fact]
    public void ResumeRequestReader_EverythingResolves_Resumes()
    {
        var pipeline = Resuming([new CheckpointCommand(CommandNames.AgenticMaster)], _serializer.Serialize(new PipelineContext()));

        Reader(_serializer).TryRead(pipeline)!.Commands.Single().Name.Should().Be(CommandNames.AgenticMaster);
    }

    private ResumeRequestReader Reader(IPipelineContextSerializer context) =>
        new(context, _commands.Object, NullLogger<ResumeRequestReader>.Instance);

    private static PipelineContext Resuming(IReadOnlyList<CheckpointCommand> commands, string contextJson)
    {
        var answer = """{"QuestionId":"q","Answer":"yes","AnsweredBy":"op","AnsweredAt":"2026-10-12T09:00:00Z"}""";
        var payload = new ResumePayload(commands, contextJson, 3, "{}", answer);
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ResumeCheckpoint, JsonSerializer.Serialize(payload));
        return pipeline;
    }
}
