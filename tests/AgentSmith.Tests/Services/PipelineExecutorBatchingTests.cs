using System.Reflection;
using AgentSmith.Application.Services;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

/// <summary>
/// p0312a: the batch path has no producer left. Batching existed so parallel
/// skill rounds of the same (Name, Round) could run concurrently; the whole
/// SkillRound command family is gone, so IsBatchableCommand matches nothing and
/// PeelBatch always yields a single command.
///
/// These tests pin that state deliberately rather than deleting the coverage:
/// the day someone makes a command batchable again they have to come here and
/// say so. Retiring the batch-execution path itself is p0312d.
/// </summary>
public class PipelineExecutorBatchingTests
{
    [Fact]
    public void IsBatchableCommand_EveryDeclaredCommand_IsNotBatchable()
    {
        var declared = typeof(CommandNames)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();
        var batchable = declared.Where(PipelineStepRunner.IsBatchableCommand).ToList();

        batchable.Should().BeEmpty(
            "the SkillRound family was the only batchable command and p0312a removed it");
    }

    [Fact]
    public void PeelBatch_NonBatchableCommand_ReturnsSingle()
    {
        var list = LinkedListOf(
            PipelineCommand.Simple(CommandNames.AnalyzeCode),
            PipelineCommand.Simple(CommandNames.AnalyzeCode));

        var batch = PipelineStepRunner.PeelBatchInternal(list.First!, maxConcurrent: 4);

        batch.Should().HaveCount(1);
    }

    [Fact]
    public void PeelBatch_MaxConcurrentOne_ReturnsSingleCommand()
    {
        var list = LinkedListOf(
            PipelineCommand.Simple(CommandNames.AnalyzeCode),
            PipelineCommand.Simple(CommandNames.AnalyzeCode));

        var batch = PipelineStepRunner.PeelBatchInternal(list.First!, maxConcurrent: 1);

        batch.Should().HaveCount(1);
    }

    private static LinkedList<PipelineCommand> LinkedListOf(params PipelineCommand[] commands) =>
        new(commands);
}
