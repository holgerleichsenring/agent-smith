using System.Text.Json;
using AgentSmith.Server.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Server;

/// <summary>
/// The wire shape the dashboard's Criteria met card reads: the endpoint writes it with the
/// web defaults, and the share and its denominator travel with the counts.
/// </summary>
public sealed class CriteriaMetSnapshotJsonTests
{
    [Fact]
    public void CriteriaMetSnapshot_Serialized_CarriesShareJudgedAndStaleCount()
    {
        var counts = new CriterionCounts(3, 1, 0, 2, 1, 1);
        var snapshot = new CriteriaMetSnapshot(1, counts, []);

        using var json = JsonDocument.Parse(
            JsonSerializer.Serialize(snapshot, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var wire = json.RootElement.GetProperty("counts");

        wire.GetProperty("judged").GetInt32().Should().Be(4);
        wire.GetProperty("share").GetDouble().Should().Be(0.75);
        wire.GetProperty("notApplicable").GetInt32().Should().Be(2);
        wire.GetProperty("staleOverrules").GetInt32().Should().Be(1);
    }

    [Fact]
    public void CriterionCounts_NothingJudged_HasNoShare()
    {
        (CriterionCounts.None with { NotApplicable = 3 }).Share.Should().BeNull();
    }
}
