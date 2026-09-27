using System.Text.RegularExpressions;
using AgentSmith.Application.Services.Configuration;
using AgentSmith.Contracts.Commands;
using FluentAssertions;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// 2026-09-25-e5b1: the four coding names p0393 retired into aliases of <c>code</c> resolve to
/// nothing now, and a name that resolves to nothing is not merely unknown — every per-preset
/// classification misses its map in silence, so the run is typed as a conversation, sized as a
/// sandbox that builds nothing and left out of the clarification park. A literal left behind in
/// the product would fail that way at RUN time, which is why it is caught here at BUILD time.
/// <para>
/// It reads string LITERALS in the backend, not prose: a comment cannot route a ticket, and the
/// history of what p0393 collapsed is worth keeping legible. A doc comment that teaches a dead
/// name is a documentation defect, judged by a reader, not by this.
/// </para>
/// </summary>
public sealed class PresetAliasRemovalTests
{
    /// <summary>The one file allowed to name them: it IS the list, and it exists so the
    /// migration and the startup finding can tell an operator what their old word became.</summary>
    private const string TheListItself = "RetiredPipelineNames.cs";

    [Fact]
    public void Presets_NoSourceInTheTree_NamesARetiredAlias()
    {
        var offenders = new List<string>();
        foreach (var file in ArchitectureSources.HandWrittenBackendFiles())
        {
            if (Path.GetFileName(file) == TheListItself) continue;
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                if (IsComment(lines[i])) continue;
                foreach (var retired in RetiredPipelineNames.Replacements.Keys)
                    if (lines[i].Contains($"\"{retired}\"", StringComparison.Ordinal))
                        offenders.Add($"{Relative(file)}:{i + 1} names '{retired}'");
            }
        }

        offenders.Should().BeEmpty(
            "a retired name resolves to nothing, so a literal one is a run that is classified "
            + "as something it is not. Name the preset it was retired into.\n  "
            + string.Join("\n  ", offenders));
    }

    [Fact]
    public void Presets_TheShippedSampleConfiguration_TeachesOnlyCurrentNames()
    {
        // The sample is what an operator copies, so a retired name in it is a configuration
        // that fails at its first ticket — and the phase that deletes the name owns the sample.
        var sample = Path.Combine(ArchitectureSources.RepositoryRoot, "config", "agentsmith.example.yml");
        var offenders = File.ReadAllLines(sample)
            .Select((line, i) => (Line: line, Number: i + 1))
            .Where(l => !l.Line.TrimStart().StartsWith('#'))
            .Where(l => RetiredPipelineNames.Replacements.Keys.Any(
                retired => Regex.IsMatch(l.Line, $"(?<![A-Za-z0-9-]){Regex.Escape(retired)}(?![A-Za-z0-9-])")))
            .Select(l => $"agentsmith.example.yml:{l.Number}: {l.Line.Trim()}")
            .ToList();

        offenders.Should().BeEmpty("\n  " + string.Join("\n  ", offenders));
    }

    [Fact]
    public void Presets_RetiredPresets_AreUntouched()
    {
        // skill-manager and autonomous are a DIFFERENT mechanism and are deliberately out of
        // this phase's scope: they have no replacement, so there is nothing to rewrite a
        // configuration to. They are also not accepted names — a configuration naming one has
        // produced a blocking finding since long before this phase, and still does.
        PipelinePresets.RetiredPresets.Keys.Should().BeEquivalentTo(["skill-manager", "autonomous"]);
        PipelinePresets.RetiredReason("skill-manager").Should().NotBeNullOrWhiteSpace();
        PipelinePresets.IsAcceptedName("skill-manager").Should().BeFalse();
        RetiredPipelineNames.ReplacementFor("autonomous").Should().BeNull(
            "a retired PRESET has no replacement; a retired NAME is the same pipeline said "
            + "differently, and only the second kind can be rewritten for the operator");
    }

    // A line whose first non-space characters open a comment. A literal is never smuggled into
    // one of those, and the p0393 history that explains the collapse lives in them.
    private static bool IsComment(string line)
    {
        var trimmed = line.TrimStart();
        return trimmed.StartsWith("//", StringComparison.Ordinal)
            || trimmed.StartsWith('*');
    }

    private static string Relative(string file) =>
        Path.GetRelativePath(ArchitectureSources.BackendRoot, file).Replace('\\', '/');
}
