using System.Reflection;
using AgentSmith.Application.Services;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// p0421: exactly ONE method decides whether a run delivered.
/// <para>
/// The operator's rule, and the reason it is worth a test: two verdicts on the same
/// question mean a wrong verdict has two possible authors. Its predecessor accreted six
/// signals and an exemption list because a second opinion always looks cheap to add —
/// and a resumed branch carrying a complete delivery still came out FAILED.
/// </para>
/// <para>
/// Discovered by TYPE, never by name: anything returning a <see cref="DeliveryVerdict"/>
/// is deciding delivery. A new decider fails this test the moment it exists.
/// </para>
/// </summary>
public sealed class OneDeliveryGateRuleTests
{
    [Fact]
    public void OnlyRunDeliveryGate_ReturnsADeliveryVerdict()
    {
        var deciders = typeof(RunDeliveryGate).Assembly.GetTypes()
            .SelectMany(t => t.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic
                | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(m => m.ReturnType == typeof(DeliveryVerdict))
            .Select(m => $"{m.DeclaringType!.Name}.{m.Name}")
            .Distinct()
            .ToList();

        deciders.Should().BeEquivalentTo([$"{nameof(RunDeliveryGate)}.{nameof(RunDeliveryGate.Evaluate)}"],
            "a second method deciding delivery is a second author for a wrong verdict — "
            + "the run's outcome is read from the accounts, in one place");
    }

    /// <summary>
    /// And no delivery decision may be taken from a pipeline PRESET. The exemption list
    /// left mad, legal, security and init with no delivery check at all; every preset is
    /// judged by its criteria now, or by nothing at all when it ratified none.
    /// </summary>
    [Fact]
    public void TheDeliveryGate_TakesTheAccountsAndTheContractSize_AndNothingElse()
    {
        var parameters = typeof(RunDeliveryGate)
            .GetMethod(nameof(RunDeliveryGate.Evaluate))!
            .GetParameters()
            .Select(p => p.ParameterType)
            .ToList();

        parameters.Should().Equal([typeof(RunAccounts), typeof(int)],
            "what a run delivered is answered by its accounts, plus how many criteria were "
            + "ratified so silence is a gap rather than a pass — a preset name, a phase-kind "
            + "flag, a master's self-report and a staged-file map are all things it used to need");
    }
}
