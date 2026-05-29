using AgentSmith.Contracts.Commands;
using FluentAssertions;

namespace AgentSmith.Tests.Commands;

/// <summary>
/// Cross-cutting invariants that span all presets. Failing means a new preset
/// landed without the matching contract, or someone removed a load step from an
/// existing one. p0137a introduced the LoadSkills-before-Triage guard after
/// FixBug / FixNoTest / AddFeature / MadDiscussion shipped with a missing
/// LoadSkills causing StructuredTriageStrategy to bail at Triage.
/// </summary>
public sealed class PipelinePresetsConsistencyTests
{
    public static IEnumerable<object[]> AllPresets =>
        PipelinePresets.Names.Select(name => new object[] { name });

    [Theory]
    [MemberData(nameof(AllPresets))]
    public void EveryPresetWithTriage_ContainsLoadSkillsBeforeIt(string presetName)
    {
        var preset = PipelinePresets.TryResolve(presetName)!.ToList();
        var triageIndex = preset.IndexOf(CommandNames.Triage);
        if (triageIndex < 0) return;

        var loadSkillsIndex = preset.IndexOf(CommandNames.LoadSkills);

        loadSkillsIndex.Should().BeGreaterThanOrEqualTo(0,
            $"preset '{presetName}' runs Triage at index {triageIndex} but does not load skills first — StructuredTriageStrategy needs AvailableRoles to assign roles");
        loadSkillsIndex.Should().BeLessThan(triageIndex,
            $"preset '{presetName}' lists LoadSkills at index {loadSkillsIndex}, after Triage at index {triageIndex}");
    }

    [Theory]
    [MemberData(nameof(AllPresets))]
    public void EveryPresetWithSkillRound_ContainsLoadSkillsBeforeIt(string presetName)
    {
        var preset = PipelinePresets.TryResolve(presetName)!.ToList();
        var skillRoundIndex = FirstIndexOfAny(preset, [
            CommandNames.SkillRound,
            CommandNames.SecuritySkillRound,
            CommandNames.ApiSecuritySkillRound,
            CommandNames.FilterRound
        ]);
        if (skillRoundIndex < 0) return;

        var loadSkillsIndex = preset.IndexOf(CommandNames.LoadSkills);

        loadSkillsIndex.Should().BeGreaterThanOrEqualTo(0,
            $"preset '{presetName}' runs a SkillRound-family command at index {skillRoundIndex} but does not load skills first");
        loadSkillsIndex.Should().BeLessThan(skillRoundIndex,
            $"preset '{presetName}' lists LoadSkills at index {loadSkillsIndex}, after SkillRound-family at index {skillRoundIndex}");
    }

    [Fact]
    public void MadDiscussion_LoadSkills_AppearsBeforeTriage()
    {
        // p0179b: coding presets (fix-bug / fix-no-test / add-feature) dropped
        // both LoadSkills and Triage when their choreography moved into the
        // coding-agent-master skill. Only mad-discussion still requires
        // LoadSkills → Triage ordering until p0179e migrates it.
        AssertLoadSkillsBeforeTriage(PipelinePresets.MadDiscussion);
    }

    private static void AssertLoadSkillsBeforeTriage(IReadOnlyList<string> preset)
    {
        var list = preset.ToList();
        var loadSkillsIndex = list.IndexOf(CommandNames.LoadSkills);
        var triageIndex = list.IndexOf(CommandNames.Triage);

        loadSkillsIndex.Should().BeGreaterThanOrEqualTo(0);
        triageIndex.Should().BeGreaterThanOrEqualTo(0);
        loadSkillsIndex.Should().BeLessThan(triageIndex);
    }

    private static int FirstIndexOfAny(IReadOnlyList<string> list, IEnumerable<string> candidates)
    {
        for (var i = 0; i < list.Count; i++)
            if (candidates.Contains(list[i]))
                return i;
        return -1;
    }
}
