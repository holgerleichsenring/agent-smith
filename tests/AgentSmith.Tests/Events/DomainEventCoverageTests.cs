using AgentSmith.Contracts.Events;
using FluentAssertions;

namespace AgentSmith.Tests.Events;

/// <summary>
/// p0173e: reflection asserts every public, non-abstract record under
/// <c>AgentSmith.Contracts.Events</c> implements <see cref="IDomainEvent"/>.
/// A new event record added without the marker fails this test on the next
/// build — the convention is enforced by the type system, not by reviewer
/// discipline.
/// </summary>
public sealed class DomainEventCoverageTests
{
    [Fact]
    public void DomainEventCoverage_AllPublicEventRecords_ImplementMarker()
    {
        var nonMarkerRecords = typeof(RunEvent).Assembly.GetTypes()
            .Where(t => t.Namespace == "AgentSmith.Contracts.Events"
                        && t.IsClass
                        && !t.IsAbstract
                        && IsRecord(t)
                        && (typeof(RunEvent).IsAssignableFrom(t)
                            || typeof(SystemEvent).IsAssignableFrom(t)))
            .Where(t => !typeof(IDomainEvent).IsAssignableFrom(t))
            .Select(t => t.FullName)
            .ToList();

        nonMarkerRecords.Should().BeEmpty(
            "every public event record must implement IDomainEvent; missing types: "
            + string.Join(", ", nonMarkerRecords));
    }

    private static bool IsRecord(Type t)
        => t.GetMethods().Any(m => m.Name == "<Clone>$");
}
