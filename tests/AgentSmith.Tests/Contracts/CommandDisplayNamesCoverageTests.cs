using System.Reflection;
using AgentSmith.Contracts.Commands;
using FluentAssertions;

namespace AgentSmith.Tests.ContractsCoverage;

/// <summary>
/// p0203: single-source-of-truth coverage test for
/// <see cref="CommandDisplayNames"/>. Reflects over every public const
/// string declared on <see cref="CommandNames"/> (and its partial-class
/// expansions Pipeline / Api / Security) and asserts each value has a
/// label in <see cref="CommandDisplayNames"/>. A hand-maintained
/// expected list would protect nothing — adding a new handler-without-
/// a-label would still pass if both the test list and the map missed it.
/// </summary>
public sealed class CommandDisplayNamesCoverageTests
{
    [Fact]
    public void CommandDisplayNamesCoverage_ReflectsCommandNamesConstants_NoConstantWithoutLabel()
    {
        var constants = ReflectCommandNameConstants();
        constants.Should().NotBeEmpty("CommandNames must declare at least one public const string");

        var missing = constants
            .Where(c => !CommandDisplayNames.All.ContainsKey(c.Value))
            .Select(c => $"{c.Name} = \"{c.Value}\"")
            .ToList();

        missing.Should().BeEmpty(
            "every public const string on CommandNames must have a key in CommandDisplayNames; missing: "
            + string.Join(", ", missing));
    }

    [Fact]
    public void CommandDisplayNames_GetReturnsCommandNameAsFallback_WhenLabelIsMissing()
    {
        const string unknownCommand = "ThisCommandHasNoLabel_p0203_test_fixture";
        CommandDisplayNames.Get(unknownCommand).Should().Be(unknownCommand);
    }

    [Fact]
    public void CommandDisplayNames_GetStripsParameterSuffix_AndResolvesBaseLabel()
    {
        var parameterised = $"{CommandNames.SkillRound}:architect:1";
        CommandDisplayNames.Get(parameterised)
            .Should().Be(CommandDisplayNames.All[CommandNames.SkillRound]);
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
