using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Progress;
using FluentAssertions;

namespace AgentSmith.Tests.Prompts;

/// <summary>2026-11-22-96c8a: no text the server sends a master names a closing tool — the master ends on its report,
/// and an older catalog that still names finish_spec is answered 'not found' by the tool layer.</summary>
public sealed class MasterTextsNameNoFinishSpecTests
{
    private static readonly string Repo = AgentSmith.Tests.Architecture.ArchitectureSources.RepositoryRoot;

    [Theory]
    [InlineData("src/backend/AgentSmith.Application/Services/Handlers/MasterClosing.cs")]
    [InlineData("src/backend/AgentSmith.Application/Services/Handlers/MasterNudges.cs")]
    [InlineData("src/backend/AgentSmith.Application/Services/Handlers/WorkingStateSection.cs")]
    [InlineData("src/backend/AgentSmith.Application/Services/SpecExecutionPromptFactory.cs")]
    [InlineData("src/backend/AgentSmith.Application/Services/SpecExecutionPromptBlocks.cs")]
    [InlineData("src/backend/AgentSmith.Application/Services/SpecWritingPromptBlocks.cs")]
    [InlineData("src/backend/AgentSmith.Application/Services/Tools/MemoryWriteToolHost.cs")]
    [InlineData("src/backend/AgentSmith.Infrastructure/Services/Providers/Agent/ContextPressureFinalizingChatClient.cs")]
    public void MasterFacingSource_NamesNoFinishSpec(string file) =>
        File.ReadAllText(Path.Combine(Repo, file)).Should().NotContain("finish_spec");

    [Fact]
    public void TheNudgesAndTheClosing_SayTheLastReplyIsTheReport()
    {
        var ledger = new ProgressLedger([new ProgressLedgerEntry("1", "a", ProgressStatus.Pending)]);
        MasterNudges.BuildReengageNudge("TASK", ledger, []).Should().Contain(MasterNudges.ReportAgain);
        MasterNudges.BuildApplyNudge("TASK", ledger).Should().Contain(MasterNudges.ReportAgain);
        MasterClosing.For("coding-agent-master", new PipelineContext()).Should().Contain("Your last reply is your report");
    }
}
