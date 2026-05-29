using System.Reflection;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Services.Events;
using FluentAssertions;

namespace AgentSmith.Tests.Events;

public sealed class SubAgentEventRoundTripTests
{
    [Fact]
    public void L2SubAgentEvents_RoundTripThroughEnvelopeSerializer()
    {
        var spawned = new SubAgentSpawnedEvent(
            "run-1", "sa-1", "ContextMapInvestigator", "map repo",
            ParentSubAgentId: null, InheritedContextHash: "sha256:abc",
            Timestamp: DateTimeOffset.Parse("2026-05-20T10:00:00Z"));
        var completed = new SubAgentCompletedEvent(
            "run-1", "sa-1", "Succeeded", 4, 1, 2, 7, 0.013m,
            DateTimeOffset.Parse("2026-05-20T10:01:00Z"));

        var spawnedBack = EventEnvelopeSerializer.Deserialize(
            EventEnvelopeSerializer.Serialize(spawned)) as SubAgentSpawnedEvent;
        var completedBack = EventEnvelopeSerializer.Deserialize(
            EventEnvelopeSerializer.Serialize(completed)) as SubAgentCompletedEvent;

        spawnedBack.Should().NotBeNull();
        spawnedBack!.Should().BeEquivalentTo(spawned);
        completedBack.Should().NotBeNull();
        completedBack!.Should().BeEquivalentTo(completed);
    }

    [Fact]
    public void L2SubAgentEvents_FrozenFixturesDeserializeOnCurrentTypes()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Events", "fixtures", "events", "L2SubAgent");
        Directory.Exists(dir).Should().BeTrue();
        var files = Directory.GetFiles(dir, "*.json");
        files.Length.Should().BeGreaterThanOrEqualTo(6);

        foreach (var file in files)
        {
            var json = File.ReadAllText(file);
            var back = EventEnvelopeSerializer.Deserialize(json);
            back.Should().NotBeNull($"fixture {Path.GetFileName(file)} must deserialise");
        }
    }

    [Fact]
    public void SubAgentResult_HasNoResultTextField_Reflection()
    {
        var props = typeof(SubAgentResult).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var hasResultText = props.Any(p => string.Equals(p.Name, "ResultText", StringComparison.OrdinalIgnoreCase));
        hasResultText.Should().BeFalse(
            "p0177 explicitly forbids a distilled-text field on SubAgentResult");
    }

    [Fact]
    public void SubAgentResult_ImplementsIDomainEvent()
    {
        typeof(IDomainEvent).IsAssignableFrom(typeof(SubAgentResult)).Should().BeTrue(
            "p0177: typed-contract discipline applies to return values too");
    }
}
