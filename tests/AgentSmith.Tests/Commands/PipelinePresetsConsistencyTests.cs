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
    public void PipelinePresets_EveryPreset_StartsWithLoadCatalog(string presetName)
    {
        var preset = PipelinePresets.TryResolve(presetName)!.ToList();

        preset.Should().NotBeEmpty();
        preset[0].Should().Be(CommandNames.LoadCatalog,
            $"preset '{presetName}' must bind the skill catalog as its first visible step (p0205)");
    }

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

    // p0179e: mad-discussion migrated off Triage → no preset still asserts
    // LoadSkills-before-Triage ordering. The helper stays in case a future
    // pipeline brings the pattern back; for now no caller invokes it.

    private static int FirstIndexOfAny(IReadOnlyList<string> list, IEnumerable<string> candidates)
    {
        for (var i = 0; i < list.Count; i++)
            if (candidates.Contains(list[i]))
                return i;
        return -1;
    }
}
