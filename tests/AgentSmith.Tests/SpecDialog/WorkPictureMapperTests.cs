using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Services.SpecDialog;
using FluentAssertions;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>2026-11-05-862ea: the work picture's column — string-enum JSON, unreadable as none, within its bound.</summary>
public sealed class WorkPictureMapperTests
{
    [Fact]
    public void SpecDialogSessionMapper_WorkPictureJson_MapsOrNullsWhenUnreadable()
    {
        var picture = new WorkPicture(UndertakingKind.Rebuild, "the handover upload", UndertakingSize.OneSpec);
        var json = SpecDialogSessionMapper.WriteWorkPicture(picture);

        json.Should().Contain("\"kind\":\"rebuild\"").And.Contain("\"size\":\"oneSpec\"");
        SpecDialogSessionMapper.ToState(Row(json)).WorkPicture.Should().Be(picture);
        SpecDialogSessionMapper.ToState(Row("""{"kind":"migration","truth":"x","size":"series"}""")).WorkPicture
            .Should().BeNull("a kind this build cannot name reads as none");
        SpecDialogSessionMapper.ToState(Row("not json")).WorkPicture.Should().BeNull();
        SpecDialogSessionMapper.ToState(Row(null)).WorkPicture.Should().BeNull();
    }

    [Fact]
    public void SpecDialogSessionMapper_WriteWorkPicture_NonAsciiTruthAtTheLimitFitsTheBound()
    {
        var truth = string.Concat(Enumerable.Repeat("ü\"", WorkPictureLimits.Truth / 2));

        var json = SpecDialogSessionMapper.WriteWorkPicture(new WorkPicture(UndertakingKind.Document, truth, UndertakingSize.Plan));

        truth.Length.Should().Be(WorkPictureLimits.Truth);
        json.Length.Should().BeLessThanOrEqualTo(WorkPictureLimits.Stored, "every escaped character still fits the column");
        SpecDialogSessionMapper.ReadWorkPicture(json)!.Truth.Should().Be(truth);
    }

    private static SpecDialogSession Row(string? workPicture) => new()
    {
        SessionId = "s-1", Platform = "dashboard", ThreadId = "d-1", ChannelId = "d-1", UserId = "person-a",
        Project = "sample", TranscriptJson = "[]", ReposJson = "[]", WorkPictureJson = workPicture,
    };
}
