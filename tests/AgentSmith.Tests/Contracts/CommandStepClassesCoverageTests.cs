using System.Reflection;
using AgentSmith.Contracts.Commands;
using FluentAssertions;

namespace AgentSmith.Tests.ContractsCoverage;

/// <summary>
/// p0398: coverage test for <see cref="CommandStepClasses"/>. Reflects over
/// every public const string declared on <see cref="CommandNames"/> (and its
/// partial-class expansions Pipeline / Api / Security) and asserts each value
/// has a display class — the same single-source-of-truth shape as
/// CommandDisplayNamesCoverageTests, so a new handler without a class is caught
/// by the suite, not by an operator wondering why a step never condenses.
/// </summary>
public sealed class CommandStepClassesCoverageTests
{
    [Fact]
    public void StepClass_EveryPipelineCommand_HasAClass()
    {
        var constants = ReflectCommandNameConstants();
        constants.Should().NotBeEmpty("CommandNames must declare at least one public const string");

        var missing = constants
            .Where(c => !CommandStepClasses.All.ContainsKey(c.Value))
            .Select(c => $"{c.Name} = \"{c.Value}\"")
            .ToList();

        missing.Should().BeEmpty(
            "every public const string on CommandNames must have a class in CommandStepClasses; missing: "
            + string.Join(", ", missing));
    }

    [Fact]
    public void StepClass_UnknownOrFutureCommand_DefaultsToMilestone_NeverSilentlyHidden()
    {
        CommandStepClasses.Get("ThisCommandHasNoClass_p0398_test_fixture")
            .Should().Be(CommandStepClasses.Milestone);
        CommandStepClasses.Get(null).Should().Be(CommandStepClasses.Milestone);
    }

    [Fact]
    public void StepClass_ParameterisedCommandName_ResolvesTheBaseClass()
    {
        CommandStepClasses.Get($"{CommandNames.SkillRound}:architect:1")
            .Should().Be(CommandStepClasses.Milestone);
        CommandStepClasses.Get($"{CommandNames.SelectPhase}:p19106a")
            .Should().Be(CommandStepClasses.Internal);
    }

    [Fact]
    public void GateNoOpSummary_KnownSentences_ClassifyAsSilent()
    {
        CommandStepClasses.IsNoOpSummary(
            CommandNames.SpecHandback, "The derivation handed nothing back").Should().BeTrue();
        CommandStepClasses.IsNoOpSummary(
            CommandNames.ScopeRepos, "Repo scoping skipped: run has no ticket").Should().BeTrue();
        CommandStepClasses.IsNoOpSummary(
            CommandNames.PlanOpenQuestions,
            "Plan complete and ticket has a body; no clarification needed").Should().BeTrue();
        CommandStepClasses.IsNoOpSummary(
            CommandNames.BootstrapGate, "Bootstrap files present in every repo.").Should().BeTrue();
        CommandStepClasses.IsNoOpSummary(
            CommandNames.PhaseSpecGate, "Phase spec p19106a validated: goal").Should().BeTrue();
        CommandStepClasses.IsNoOpSummary(
            CommandNames.ScopeRepos, "Scoped run to 2 of 5 repos: api, worker").Should().BeFalse();
        CommandStepClasses.IsNoOpSummary(
            CommandNames.EmptyPlanCheck, "empty-plan-skip: reason=empty_plan").Should().BeFalse();
    }

    private static IReadOnlyList<(string Name, string Value)> ReflectCommandNameConstants()
    {
        return typeof(CommandNames)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
            .Select(f => (f.Name, (string)f.GetRawConstantValue()!))
            .ToList();
    }
}
