using AgentSmith.Application.Services.SpecDialog;
using FluentAssertions;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>2026-11-12-cd4cd: a work picture's truth is folded to one line and has no bound but the stored column's.</summary>
public sealed class WorkPictureTruthFoldTests
{
    [Fact]
    public void WorkPictureAdmission_MultiLineLongTruth_IsFoldedAndStored()
    {
        var truth = "repo-a.\nAnd repo-b. " + new string('x', 600);

        var (picture, refusal) = WorkPictureAdmission.Of("fix", truth, "one-spec");

        refusal.Should().BeNull();
        picture!.Truth.Should().StartWith("repo-a. And repo-b. ").And.NotContain("\n");
    }

    [Fact]
    public void WorkPictureAdmission_TruthOverTheStoredColumn_IsRefusedNamingIt() =>
        WorkPictureAdmission.Of("fix", new string('x', 3000), "one-spec").Refusal.Should().Contain("the stored picture holds 2000");
}
