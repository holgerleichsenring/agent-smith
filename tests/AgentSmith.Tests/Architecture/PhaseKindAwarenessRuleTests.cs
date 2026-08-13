using System.Reflection;
using System.Runtime.CompilerServices;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// p0406: a method that decides whether a run DELIVERED must know what the phase set out
/// to deliver. p0400 taught the keystone, the verify gate and the sandbox baseline about
/// <c>ships_code</c> and missed the open loop's acceptance gate — the third consumer found
/// blind to the phase kind, and the one that burned run fa8c for 41 minutes.
/// <para>
/// A decider is discovered by TYPE, never by name: it consumes the master's verdict (a
/// <see cref="MasterVerification"/> or its <see cref="AcceptanceDisposition"/> list) and
/// returns a verdict of its own (<c>bool</c> or <see cref="KeystoneVerdict"/>). Each one
/// must take the phase kind. <see cref="PhaseKindFreeDecisions"/> names the exceptions;
/// entries are <c>typeof</c> + <c>nameof</c>, so a rename follows the code, and
/// <see cref="EveryExemption_StillNamesAPhaseKindFreeDecider"/> fails when one goes stale.
/// </para>
/// </summary>
public sealed class PhaseKindAwarenessRuleTests
{
    private const string PhaseKindParameter = "shipsCode";

    // Decisions that read the verdict WITHOUT judging delivery — the phase kind would be an
    // unused parameter, and an unused parameter is a claim the method does not honour.
    private static readonly (Type Type, string Method)[] PhaseKindFreeDecisions =
    [
        // The build/test gate: whether a verdict exists and its build passed. Whether the
        // phase OWED a build is the caller's question (expectsGreenTests).
        (typeof(RunOutcomeKeystone), "EvaluateVerificationGate"),
        // Regression math over the reported failing-test lists — final minus baseline.
        (typeof(RunOutcomeKeystone), "EvaluateTests"),
        // Asks whether a verdict is MISSING, not whether the contract is met.
        (typeof(MasterReengagementPolicy), nameof(MasterReengagementPolicy.ShouldNudgeForVerdict)),
        // Bounds re-driving a silent master; a null verdict is silence for either phase kind.
        (typeof(MasterAcceptanceGate), nameof(MasterAcceptanceGate.VerdictlessAfterOneRedrive)),
    ];

    [Fact]
    public void EveryAcceptanceDecision_TakesThePhaseKind()
    {
        var exempt = PhaseKindFreeDecisions.Select(Resolve).ToHashSet();
        var blind = Deciders()
            .Where(m => !exempt.Contains(m) && !TakesPhaseKind(m))
            .Select(Describe).OrderBy(x => x, StringComparer.Ordinal).ToList();

        blind.Should().BeEmpty(
            $"a delivery decision that cannot see `{PhaseKindParameter}` judges a knowledge "
            + "phase by the source change it declared it would not make. Take the phase kind "
            + "(Specs.PhaseDelivery.ShipsCode) or classify the method as phase-kind-free.\n  "
            + string.Join("\n  ", blind));
    }

    [Fact]
    public void EveryExemption_StillNamesAPhaseKindFreeDecider()
    {
        var deciders = Deciders().ToHashSet();
        var stale = PhaseKindFreeDecisions.Select(Resolve)
            .Where(m => !deciders.Contains(m) || TakesPhaseKind(m))
            .Select(Describe).OrderBy(x => x, StringComparer.Ordinal).ToList();

        stale.Should().BeEmpty(
            "an exemption that no longer describes a phase-kind-free decision stops being a "
            + "classification and becomes a place to hide. Remove the entry.\n  "
            + string.Join("\n  ", stale));
    }

    [Fact]
    public void Sanity_TheScanReachesTheKnownDecisionPoints()
    {
        var deciders = Deciders().ToHashSet();

        deciders.Should().Contain(Resolve((typeof(RunOutcomeKeystone), nameof(RunOutcomeKeystone.Evaluate))));
        deciders.Should().Contain(Resolve((typeof(MasterAcceptanceGate), "ObjectivelySatisfied")));
        deciders.Should().Contain(Resolve((typeof(MasterReengagementPolicy), "ShouldReengage")));
        deciders.Should().HaveCountGreaterThan(4, "a scan that finds nothing passes silently");
    }

    [Fact]
    public void Rule_HasTeeth_ADeciderWithoutThePhaseKindIsRecognised()
    {
        TakesPhaseKind(Resolve((typeof(MasterAcceptanceGate), "ObjectivelySatisfied"))).Should().BeTrue();
        TakesPhaseKind(Resolve((typeof(MasterAcceptanceGate), "VerdictlessAfterOneRedrive"))).Should().BeFalse();
        IsVerdictInput(typeof(MasterVerification)).Should().BeTrue();
        IsVerdictInput(typeof(IReadOnlyList<AcceptanceDisposition>)).Should().BeTrue();
        IsVerdictInput(typeof(string)).Should().BeFalse("only the master's verdict makes a decider");
    }

    private static MethodInfo Resolve((Type Type, string Method) entry)
    {
        var found = entry.Type.GetMethods(AllDeclared).Where(m => m.Name == entry.Method).ToList();
        found.Should().ContainSingle(
            $"{entry.Type.Name}.{entry.Method} must still exist exactly once for the entry to mean something");
        return found[0];
    }

    // Application holds every rule that judges a run's delivery; the verdict types it reads
    // live in Domain and are data, so scanning the deciding assembly is the whole surface.
    private static IEnumerable<MethodInfo> Deciders() =>
        typeof(RunOutcomeKeystone).Assembly.GetTypes()
            .SelectMany(t => t.GetMethods(AllDeclared))
            .Where(m => !m.Name.Contains('<', StringComparison.Ordinal)
                        && !m.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false))
            .Where(m => m.ReturnType == typeof(bool) || m.ReturnType == typeof(KeystoneVerdict))
            .Where(m => m.GetParameters().Any(p => IsVerdictInput(p.ParameterType)));

    // The master's verdict, in either shape it travels: the whole record, or the
    // per-criterion dispositions the acceptance rules actually read.
    private static bool IsVerdictInput(Type t) =>
        t == typeof(MasterVerification)
        || (t.IsGenericType && t.GetGenericArguments() is [var arg] && arg == typeof(AcceptanceDisposition));

    private static bool TakesPhaseKind(MethodInfo m) =>
        m.GetParameters().Any(p => p.ParameterType == typeof(bool) && p.Name == PhaseKindParameter);

    private static string Describe(MethodInfo m) => $"{m.DeclaringType?.FullName}.{m.Name}";

    private const BindingFlags AllDeclared =
        BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public
        | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
}
