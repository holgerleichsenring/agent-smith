using System.Text.Json;
using System.Text.Json.Serialization;
using AgentSmith.Domain.Entities;
using FluentAssertions;

namespace AgentSmith.Tests.Domain;

public sealed class DiffRecordTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
    };

    [Fact]
    public void Diff_AllFieldsSet_RoundTripThroughJson()
    {
        var original = new Diff(
            new[] { new DiffChange("src/Foo.cs", DiffOperation.Modify, "tweak", "@@") },
            new[] { new DiffTestEntry("tests/FooTests.cs", "added") },
            new[] { new DiffTestEntry("tests/BarTests.cs", "renamed") },
            DiffStatus.Ok,
            DiffStatus.Failed);

        var json = JsonSerializer.Serialize(original, Options);
        var roundTripped = JsonSerializer.Deserialize<Diff>(json, Options);

        roundTripped.Should().BeEquivalentTo(original);
    }

    [Fact]
    public void DiffOperation_EnumValues_MapToSchema()
    {
        Enum.GetNames(typeof(DiffOperation)).Should().BeEquivalentTo(
            new[] { "Modify", "Add", "Delete" });
    }

    [Fact]
    public void DiffStatus_EnumValues_MapToSchema()
    {
        Enum.GetNames(typeof(DiffStatus)).Should().BeEquivalentTo(
            new[] { "Ok", "Failed", "NotRun" });
    }
}
