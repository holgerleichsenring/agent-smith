using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Services.Events;
using FluentAssertions;

namespace AgentSmith.Tests.Events;

/// <summary>
/// 2026-09-03-b028: p0405 wrote that <see cref="EventTypeResolver"/> "grows by one line
/// every time an event is added". Nothing checked it, and two events were added without
/// one — PullRequestOutcome and TicketInstructionIgnored were published, crossed the
/// transport and were dropped on arrival, so every run snapshot carried prUrl null even
/// for runs that opened a real pull request.
/// <para>
/// This test reads <c>Enum.GetValues</c>, not a copy of the enum, so it cannot drift from
/// what producers can publish. A value that is deliberately unresolvable belongs in the
/// named lists below with the reason — both are EMPTY, which is the intended state: an
/// event the reader cannot rebuild is indistinguishable from an event nobody sent.
/// </para>
/// </summary>
public sealed class EventTypeResolverCoverageTests
{
    private static readonly EventType[] UnresolvedByDesign = [];

    private static readonly SystemEventType[] UnresolvedSystemByDesign = [];

    [Fact]
    public void EveryEventType_ResolvesToARecord()
    {
        var missing = Enum.GetValues<EventType>()
            .Where(t => !UnresolvedByDesign.Contains(t))
            .Where(t => EventTypeResolver.Resolve(t) is null)
            .Select(t => $"{t} ({(int)t})")
            .ToList();

        missing.Should().BeEmpty(
            "an EventType with no resolver row is published, crosses the transport and is "
            + "dropped on arrival. Add one line to EventTypeResolver.Resolve.\n  "
            + string.Join("\n  ", missing));
    }

    [Fact]
    public void EverySystemEventType_ResolvesToARecord()
    {
        var missing = Enum.GetValues<SystemEventType>()
            .Where(t => !UnresolvedSystemByDesign.Contains(t))
            .Where(t => EventTypeResolver.ResolveSystem(t) is null)
            .Select(t => $"{t} ({(int)t})")
            .ToList();

        missing.Should().BeEmpty(
            "a SystemEventType with no resolver row never reaches the system feed.\n  "
            + string.Join("\n  ", missing));
    }

    [Fact]
    public void EveryResolvedRecord_IsTheHierarchyItsCallerExpects()
    {
        foreach (var type in Enum.GetValues<EventType>().Except(UnresolvedByDesign))
            EventTypeResolver.Resolve(type)!
                .Should().BeAssignableTo<RunEvent>($"{type} is read back as a RunEvent");

        foreach (var type in Enum.GetValues<SystemEventType>().Except(UnresolvedSystemByDesign))
            EventTypeResolver.ResolveSystem(type)!
                .Should().BeAssignableTo<SystemEvent>($"{type} is read back as a SystemEvent");
    }

    [Fact]
    public void AnUnknownCode_ResolvesToNothing_SoTheRuleHasTeeth()
    {
        EventTypeResolver.Resolve((EventType)9999).Should().BeNull();
        EventTypeResolver.ResolveSystem((SystemEventType)9999).Should().BeNull();
    }
}
