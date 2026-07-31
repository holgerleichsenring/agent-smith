using System.Text.RegularExpressions;
using AgentSmith.Contracts.Commands;
using FluentAssertions;

namespace AgentSmith.Tests.Commands;

/// <summary>
/// p0312c: every preset must be able to publish its own name.
///
/// PipelineNameInitializer publishes ContextKeys.PipelineName through
/// IRunStateConcepts.SetEnum, which throws hard on a value the catalog's
/// pipeline_name enum does not declare. An undeclared preset therefore dies at
/// step two of every run it ever attempts — which is exactly what happened to
/// pr-review from p0167a until p0312c, unnoticed for months.
///
/// It went unnoticed because the only test touching the concept set it against
/// a STUB vocabulary (RunStateConceptsTestFactory.FallbackMinimal) and proved
/// the roster filter, not the composition. This test reads the catalog file
/// instead.
///
/// Scope note: the file read here is the harness's checked-in catalog fixture.
/// Asserting the same against the PINNED tarball is part of the 4.0.0 pin bump
/// (p0312b's release-and-pin step) — the currently pinned v3.29.0 predates both
/// `code` and `pr-review`, so that half cannot pass until the pin moves.
/// </summary>
public sealed class PipelineNameVocabularyTests
{
    [Fact]
    public void EveryPresetThatPublishesItsName_IsDeclaredInThePipelineNameEnum()
    {
        var declared = ReadPipelineNameEnum();

        // Only presets that actually run the step need the declaration.
        // spec-dialog and phase-execution deliberately omit it — they run no
        // skill activation, and requiring a catalog bump would have broken them
        // on every existing pin.
        var publishers = PipelinePresets.Names
            .Where(n => PipelinePresets.TryResolve(n)!
                .Contains(CommandNames.PipelineNameInitializer, StringComparer.Ordinal))
            .ToList();

        publishers.Should().BeSubsetOf(declared,
            "a preset whose name the catalog does not declare throws in "
            + "PipelineNameInitializer and can never complete a single run");
    }

    [Fact]
    public void RetiredPresets_AreNotDeclared()
    {
        var declared = ReadPipelineNameEnum();

        declared.Should().NotContain(PipelinePresets.RetiredPresets.Keys,
            "a retired preset left in the vocabulary invites a configuration "
            + "that resolves to nothing");
    }

    private static IReadOnlyList<string> ReadPipelineNameEnum()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory, "Fixtures", "SkillsCatalog", "skills", "concept-vocabulary.yaml");
        if (!File.Exists(path))
            path = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", "AgentSmith.PipelineHarness",
                "Fixtures", "SkillsCatalog", "skills", "concept-vocabulary.yaml"));
        File.Exists(path).Should().BeTrue($"the catalog fixture must be readable at {path}");

        var text = File.ReadAllText(path);
        var match = Regex.Match(
            text, @"-\s*name:\s*pipeline_name.*?enum_values:\s*\[(?<values>[^\]]*)\]",
            RegexOptions.Singleline);
        match.Success.Should().BeTrue("the catalog must declare a pipeline_name enum");

        return match.Groups["values"].Value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }
}
