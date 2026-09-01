using System.Text.Json;
using System.Text.Json.Nodes;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Xunit.Abstractions;

namespace AgentSmith.Tests.Tools;

/// <summary>
/// 2026-08-26-167c: one rejection carries every defect, each rule quoted once, bounded
/// by characters.
/// <para>
/// Three defects at a time cannot converge inside a five-refusal budget: a realistic
/// off-vocabulary document carries about twenty. And the 400-character cap on a quoted
/// rule silently DROPPED the <c>arch.patterns</c> list (486 characters), so the one
/// field with 36 values was the one field whose values the model never saw — the
/// guessing the rejection exists to end, still live.
/// </para>
/// </summary>
public sealed class ContextDefectReportTests(ITestOutputHelper output)
{
    private const string ManyDefects = """
        {
          "meta": { "workdir": ".", "type": ["todo", "x", "a-far-too-long-archetype-name-for-this-field"] },
          "stack": { "lang": "C#", "image": "node:20-bookworm" },
          "arch": {
            "style": ["tbd"],
            "patterns": ["todo", "tbd", "unknown", "n/a", "???", "xxx"]
          },
          "quality": { "lang": "denglisch", "testing": { "style": "?" } }
        }
        """;

    [Fact]
    public void Report_ADocumentWithManyDefects_NamesThemAll()
    {
        var report = Report(ManyDefects);
        output.WriteLine(report);

        report.Should()
            .Contain("/meta/type/0").And.Contain("/meta/type/2")
            .And.Contain("/arch/style/0")
            .And.Contain("/arch/patterns/0").And.Contain("/arch/patterns/5")
            .And.Contain("/quality/lang")
            .And.Contain("/quality/testing/style",
                "three at a time cannot converge inside a five-refusal budget");
    }

    [Fact]
    public void Report_SixDefectsOnOneField_QuotesTheRuleOnce()
    {
        var report = Report(ManyDefects);

        Occurrences(report, "UnitOfWork").Should().Be(1,
            "six values broke ONE rule — quoting its 36 suggestions six times is how a "
            + "rejection stops being readable");
        report.Should().Contain("/arch/patterns/0, /arch/patterns/1",
            "the locations sit together on the line that quotes their rule");
    }

    [Fact]
    public void Report_ALongSuggestionList_IsTruncatedRatherThanDropped()
    {
        var report = Report(ManyDefects);

        report.Should().Contain("\"Repository\"", "the head of the list is shown")
            .And.Contain("and 28 more",
                "36 suggestions minus the 8 shown — dropping them was the old behaviour, "
                + "and it left the model with a keyword and no values");
    }

    [Fact]
    public void Report_AnImageDefectAndASchemaDefect_AreBothReported()
    {
        // The gate returned one OR the other, so the commonest first-round failure hid
        // every schema defect behind a single line.
        var gate = ContextGates.Build();
        var document = JsonDocument.Parse("""
            { "meta": { "workdir": ".", "type": ["todo"] } }
            """).RootElement;

        gate.TryRead(document, out var typed, out _).Should().BeTrue();
        var defect = gate.Defect(typed!);

        defect.Should().Contain("/stack:", "the image rule still speaks")
            .And.Contain("/meta/type/0", "and no longer silences the schema");
    }

    [Fact]
    public void Report_AnOversizedReport_IsBoundedByCharacters()
    {
        var defects = Enumerable.Range(0, 400)
            .Select(index => new ContextSchemaDefect(
                $"/field{index}", "pattern", new string('x', 120), $"/nowhere/{index}"))
            .ToList();

        var report = ContextGates.DefectReport().Compose(null, defects)!;

        report.Length.Should().BeLessThan(4500,
            "a tool result the model has to read is bounded by size, not by a count of "
            + "defects that says nothing about how long each one is");
        report.Should().Contain("further defect(s) not shown",
            "what was left out is stated, never silently dropped");
    }

    // 2026-09-01-0f80: a key the schema does not declare is refused by
    // additionalProperties, whose schema node is the BOOLEAN false. Reading a description
    // off it threw, so the author who misspelled one field got a stack trace where the
    // report exists to hand them the field's name.
    private const string UndeclaredField = """
        {
          "meta": { "workdir": "." },
          "verify": [ { "stage": "build", "command": "dotnet build" } ]
        }
        """;

    [Fact]
    public void DefectReport_AnUnknownField_NamesTheKeyAndItsPath()
    {
        var report = Report(UndeclaredField);
        output.WriteLine(report);

        report.Should().Contain("/verify/0/stage: \"stage\" is not a field this schema declares",
            "the author who wrote stage where the schema declares label learns WHICH field "
            + "is wrong — the validator's own wording names neither the key nor its path");
    }

    [Fact]
    public void DefectReport_ADeclaredFieldWithABadValue_ReportsAsBefore()
    {
        var report = Report("""
            {
              "meta": { "workdir": "." },
              "verify": [ { "label": "b", "command": "dotnet build" } ]
            }
            """);
        output.WriteLine(report);

        report.Should().Contain("/verify/0/label")
            .And.Contain("schema minLength: 2", "the broken rule is still quoted back")
            .And.Contain("\"schema-check\"", "and so are its suggestions");
    }

    [Fact]
    public void Report_AValidDocument_IsNotAReport() =>
        ContextGates.DefectReport().Compose(null, []).Should().BeNull();

    private static string Report(string json) =>
        ContextGates.Rule().Defect(JsonNode.Parse(json))!;

    private static int Occurrences(string text, string needle) =>
        text.Split(needle, StringSplitOptions.None).Length - 1;
}
