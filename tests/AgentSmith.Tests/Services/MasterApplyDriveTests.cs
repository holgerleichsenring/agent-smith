using AgentSmith.Application.Services.Handlers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

// p0255: when a code-expecting run wrote a plan but edited no source, the master
// is re-prompted once to APPLY it. These pin the decision that triggers the
// re-prompt (the recurring "planned, then stopped, shipped nothing" run).
public sealed class MasterApplyDriveTests
{
    private static CodeChange Change(string path) => new(new FilePath(path), "x", "Modify");

    [Fact]
    public void ShouldDriveApply_CodePreset_OnlyRunRecordWrites_True()
    {
        var changes = new List<CodeChange> { Change(".agentsmith/plan.md"), Change(".agentsmith/decisions.md") };
        AgenticMasterHandler.ShouldDriveApply("fix-bug", changes).Should().BeTrue();
    }

    [Fact]
    public void ShouldDriveApply_CodePreset_NoWritesAtAll_True()
    {
        AgenticMasterHandler.ShouldDriveApply("fix-bug", new List<CodeChange>()).Should().BeTrue();
    }

    [Fact]
    public void ShouldDriveApply_CodePreset_HasRealSourceEdit_False()
    {
        var changes = new List<CodeChange> { Change("src/Controllers/AppController.cs"), Change(".agentsmith/plan.md") };
        AgenticMasterHandler.ShouldDriveApply("fix-bug", changes).Should().BeFalse();
    }

    [Fact]
    public void ShouldDriveApply_ReadOnlyPreset_NoEdits_False()
    {
        // security/legal/mad/init legitimately finish with zero changes — never re-prompt.
        AgenticMasterHandler.ShouldDriveApply("security-scan", new List<CodeChange>()).Should().BeFalse();
    }

    [Fact]
    public void ShouldDriveApply_NoPipelineName_False()
    {
        AgenticMasterHandler.ShouldDriveApply(null, new List<CodeChange>()).Should().BeFalse();
    }
}
