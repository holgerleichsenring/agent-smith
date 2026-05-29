using System.Reflection;
using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Services.Events;
using FluentAssertions;

namespace AgentSmith.Tests.Events;

/// <summary>
/// p0173e rule (a) — frozen JSON fixtures under <c>fixtures/events/&lt;tier&gt;/</c>
/// must deserialise on the current event types via the same
/// <see cref="EventEnvelopeSerializer"/> used in production. A breaking field
/// rename / type change surfaces here as a deserialisation null or an
/// exception, and the failure message identifies the offending fixture file.
/// </summary>
public sealed class EventSchemaCompatibilityTests
{
    private static readonly string FixturesRoot = Path.Combine(
        AppContext.BaseDirectory, "Events", "fixtures", "events");

    public static IEnumerable<object[]> RunEventFixtures()
        => EnumerateFixtures(includeSystem: false).Select(f => new object[] { f });

    public static IEnumerable<object[]> SystemEventFixtures()
        => EnumerateFixtures(includeSystem: true)
            .Where(f => Path.GetFileName(Path.GetDirectoryName(f)) == "System")
            .Select(f => new object[] { f });

    [Theory]
    [MemberData(nameof(RunEventFixtures))]
    public void EventSchemaCompatibility_FrozenFixtures_DeserializeOnCurrentTypes(string fixturePath)
    {
        var json = File.ReadAllText(fixturePath);
        if (IsSystemFixture(fixturePath))
        {
            var systemEvent = EventEnvelopeSerializer.DeserializeSystem(json);
            systemEvent.Should().NotBeNull(
                $"system fixture {Path.GetFileName(fixturePath)} must deserialise on current types");
            return;
        }
        var runEvent = EventEnvelopeSerializer.Deserialize(json);
        runEvent.Should().NotBeNull(
            $"run fixture {Path.GetFileName(fixturePath)} must deserialise on current types");
    }

    [Fact]
    public void EventSchemaCompatibility_AddedFieldMustBeOptionalWithDefault_GuardTest()
    {
        var contractsAssembly = typeof(RunEvent).Assembly;
        var allRecords = contractsAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Namespace == "AgentSmith.Contracts.Events"
                        && (typeof(RunEvent).IsAssignableFrom(t) || typeof(SystemEvent).IsAssignableFrom(t)))
            .ToList();

        var ctorsWithoutOptionalDefaults = allRecords
            .SelectMany(r => r.GetConstructors())
            .Where(c => c.GetParameters().Any(p => p.IsOptional && !p.HasDefaultValue))
            .ToList();

        ctorsWithoutOptionalDefaults.Should().BeEmpty(
            "every optional ctor parameter on an event record must carry an explicit default value (rule (a))");
    }

    [Fact]
    public void DeprecatedField_RemainsReadable_ExampleEvent()
    {
        var deprecatedProperties = typeof(RunEvent).Assembly.GetTypes()
            .Where(t => t.Namespace == "AgentSmith.Contracts.Events")
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            .Where(p => p.GetCustomAttribute<DeprecatedFieldAttribute>() is not null)
            .ToList();

        foreach (var prop in deprecatedProperties)
        {
            prop.CanRead.Should().BeTrue(
                $"deprecated field {prop.DeclaringType?.Name}.{prop.Name} must remain readable (rule (b))");
        }
    }

    private static IEnumerable<string> EnumerateFixtures(bool includeSystem)
    {
        if (!Directory.Exists(FixturesRoot)) return Array.Empty<string>();
        return Directory.EnumerateFiles(FixturesRoot, "*.json", SearchOption.AllDirectories)
            .OrderBy(p => p, StringComparer.Ordinal);
    }

    private static bool IsSystemFixture(string fixturePath)
        => Path.GetFileName(Path.GetDirectoryName(fixturePath)) == "System";
}
