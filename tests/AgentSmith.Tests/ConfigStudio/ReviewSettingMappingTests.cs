using System.Text.Json.Nodes;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Migration.Services.ConfigDocuments;
using FluentAssertions;

namespace AgentSmith.Tests.ConfigStudio;

/// <summary>2026-11-25-9992i: the mapping of one stored merge block — every row of the table, the changes it names, and the
/// keys it drops whatever their spelling.</summary>
public sealed class ReviewSettingMappingTests
{
    [Theory]
    [InlineData("""{"SeriesWhen":0,"When":2}""", ReviewMode.Each)]
    [InlineData("""{"seriesWhen":"Manual"}""", ReviewMode.Each)]
    [InlineData("""{"When":0}""", ReviewMode.End)]
    [InlineData("""{"RespectBranchProtection":true}""", ReviewMode.End)]
    [InlineData("""{"When":"on_ci_green"}""", ReviewMode.None)]
    [InlineData("""{"when":1,"SeriesWhen":2}""", ReviewMode.None)]
    public void Of_MapsEveryRow(string merge, ReviewMode expected) => ReviewSettingMapping.Of(Block(merge)).Should().Be(expected);

    [Fact]
    public void Changes_NameWhatAStandaloneTicketNowDoesOtherwise()
    {
        ReviewSettingMapping.Changes(Block("""{"When":2,"SeriesWhen":0}"""), ReviewMode.Each).Should().ContainSingle()
            .Which.Should().Contain("wait for a person");
        ReviewSettingMapping.Changes(Block("""{"When":1}"""), ReviewMode.None).Should().HaveCount(2);
        ReviewSettingMapping.Changes(Block("""{"When":2,"RespectBranchProtection":true}"""), ReviewMode.None).Should().BeEmpty();
        ReviewSettingMapping.Changes(Block("""{"When":0}"""), ReviewMode.End).Should().BeEmpty();
    }

    [Fact]
    public void Names_And_Drop_TheRetiredKeysInAnySpelling_KeepingTheRest()
    {
        var merge = Block("""{"when":2,"SERIESWHEN":0,"RespectBranchProtection":true,"refreshopenpullrequests":false,"CiPollSeconds":30}""");

        ReviewSettingMapping.Names(merge).Should().BeTrue();
        ReviewSettingMapping.Drop(merge);

        merge.Select(p => p.Key).Should().Equal("CiPollSeconds");
        ReviewSettingMapping.Names(merge).Should().BeFalse();
    }

    private static JsonObject Block(string json) => JsonNode.Parse(json)!.AsObject();
}
