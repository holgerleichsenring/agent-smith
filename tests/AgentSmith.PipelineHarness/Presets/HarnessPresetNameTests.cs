using System.Text.RegularExpressions;
using AgentSmith.Contracts.Commands;
using FluentAssertions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-09-25-a7e8: the harness asks for presets by the names they HAVE.
///
/// <para>
/// Sixteen suites used to ask for one of the four coding names p0393 retired into aliases
/// of <c>code</c>. PipelineRunner throws on a name that does not resolve, so the phase
/// that deletes the alias map would otherwise have produced a tree
/// whose own gate could not go green. These tests hold the harness on the far side of that
/// move: a suite added between now and the deletion fails HERE, in a test that names the
/// problem, instead of in a gate run that reports sixteen unrelated failures.
/// </para>
/// <para>
/// The forbidden names are read from <see cref="PipelinePresets.PresetAliases"/> rather
/// than listed again here — a second copy of a list that is about to be emptied would
/// outlive its subject and start lying.
/// </para>
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class HarnessPresetNameTests
{
    /// <summary>Where the suites live. Composition/ helpers own RunAsync calls of their
    /// own (git, docker) that have nothing to do with presets.</summary>
    private static readonly string[] SuiteFolders = ["Presets", "Replay"];

    /// <summary>A preset name as a suite hands it over: the first string argument of a
    /// RunAsync / StartAsync call.</summary>
    private const string PresetAsk = @"(?:Run|Start)Async\(\s*""([^""]*)""";

    [Fact]
    public void Harness_NoSuite_NamesARetiredAlias()
    {
        var offenders = new List<string>();
        foreach (var file in HarnessSources())
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
                foreach (var alias in PipelinePresets.PresetAliases.Keys)
                    if (Regex.IsMatch(lines[i], $"(?<![A-Za-z0-9-]){Regex.Escape(alias)}(?![A-Za-z0-9-])"))
                        offenders.Add($"{Relative(file)}:{i + 1} names '{alias}'");
        }

        offenders.Should().BeEmpty(
            "a retired alias resolves only while PipelinePresets.PresetAliases still carries "
            + "it. Ask for the preset the alias resolves to, and say what the SCENARIO is in "
            + "words.\n  " + string.Join("\n  ", offenders));
    }

    [Fact]
    public void Harness_EverySuite_ResolvesThePresetItAsksFor()
    {
        // A retired PRESET (PipelinePresets.RetiredPresets) is a different debt with its own
        // owner — AutonomousDockerTests still asks for 'autonomous', which p0312a retired,
        // and has been unrunnable ever since. Naming it here would only hide it behind this
        // phase's verdict.
        var asked = AskedPresets().ToList();
        asked.Should().NotBeEmpty(
            "a scan that finds no preset ask at all has stopped reading the suites, and an "
            + "assertion over nothing passes for the wrong reason");

        var unresolved = asked
            .Where(name => PipelinePresets.TryResolve(name) is null)
            .Where(name => PipelinePresets.RetiredReason(name) is null)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        unresolved.Should().BeEmpty(
            "PipelineRunner throws on a preset name it cannot resolve, so a suite asking for "
            + "one is dead weight that only shows up when the tier it sits in runs.\n  "
            + string.Join("\n  ", unresolved));
    }

    /// <summary>Every preset name a suite passes to a runner or to the docker scaffolding.</summary>
    private static IEnumerable<string> AskedPresets() =>
        HarnessSources()
            .Where(f => SuiteFolders.Any(
                folder => f.Contains(Path.DirectorySeparatorChar + folder + Path.DirectorySeparatorChar,
                    StringComparison.Ordinal)))
            .SelectMany(f => Regex.Matches(File.ReadAllText(f), PresetAsk))
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.Ordinal);

    private static IEnumerable<string> HarnessSources() =>
        Directory.EnumerateFiles(HarnessRoot, "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                            StringComparison.Ordinal)
                        && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                            StringComparison.Ordinal))
            .OrderBy(p => p, StringComparer.Ordinal);

    private static string Relative(string file) =>
        Path.GetRelativePath(HarnessRoot, file).Replace('\\', '/');

    /// <summary>The harness's own source tree, found by walking out of bin/.</summary>
    private static string HarnessRoot { get; } = ResolveHarnessRoot();

    private static string ResolveHarnessRoot()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 10; i++)
        {
            if (File.Exists(Path.Combine(dir, "AgentSmith.PipelineHarness.csproj"))) return dir;
            var parent = Directory.GetParent(dir);
            if (parent is null) break;
            dir = parent.FullName;
        }
        throw new InvalidOperationException(
            $"Could not locate the harness project from '{AppContext.BaseDirectory}'");
    }
}
