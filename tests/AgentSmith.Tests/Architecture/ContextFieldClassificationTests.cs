using System.Reflection;
using System.Text.Json.Nodes;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Infrastructure.Services;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// 2026-08-26-04b6: every context field is classified in the schema itself — JUDGEMENT
/// (somebody decided it), MECHANISM (the orchestrator acts on it) or READING (a copy of
/// something the repository still states, wrong the day after it is written).
/// <para>
/// The classification is the deliverable, so it is checkable rather than remembered: the
/// field set is DERIVED from the schema, and a field added without a classification fails
/// here instead of being assumed into the file.
/// </para>
/// <para>
/// The readings are DEPRECATED, never deleted. The schema root is
/// <c>additionalProperties: false</c>, so removing a property would not stop asking for it —
/// it would start REFUSING it, and every context written before this phase would fail on its
/// first run, this repository's own included.
/// </para>
/// </summary>
public sealed class ContextFieldClassificationTests
{
    private static readonly string[] Classes = ["JUDGEMENT", "MECHANISM", "READING"];

    /// <summary>The blocks whose CHILDREN are the fields; the block itself is a container.</summary>
    private static readonly string[] Blocks = ["meta", "stack", "arch", "behavior", "quality"];

    /// <summary>A context in the shape the writer emitted before this phase — every reading.</summary>
    private const string PreviousShape = """
        meta:
          workdir: "."
          project: "sample"
          version: "1.0.0"
          repo: "https://example.invalid/sample.git"
          type: ["api"]
          purpose: "Serves orders to the storefront."
        stack:
          lang: "C#"
          runtime: ".NET 8"
          image: "mcr.microsoft.com/dotnet/sdk:8.0"
          frameworks: ["ASP.NET-Core"]
          frontend: ["React"]
          infra: ["Docker"]
          testing: ["xUnit"]
          ci: ["GitHub-Actions"]
          sdks: ["SomeLib@12.3.0"]
        prerequisites: "npm ci"
        arch:
          style: ["Layered"]
          patterns: ["Repository"]
          layers: ["Domain", "Application"]
          bounded-contexts: ["Ordering"]
        quality:
          lang: "english-only"
          limits: { method-lines: 20 }
          principles: ["SOLID", "DRY"]
        state:
          done:
            p0001: "shipped the first thing"
          active: {}
        """;

    [Fact]
    public void Schema_EveryFieldIsClassified()
    {
        var unclassified = Fields()
            .Where(field => Classes.Count(word => Marker(field.Node, word)) != 1)
            .Select(field => field.Pointer)
            .OrderBy(pointer => pointer, StringComparer.Ordinal)
            .ToList();

        unclassified.Should().BeEmpty(
            "every field carries exactly one of JUDGEMENT / MECHANISM / READING in its "
            + "$comment, with one clause saying why — a field nobody classified is a field "
            + "nobody argued for.\n  " + string.Join("\n  ", unclassified));
    }

    [Fact]
    public void Schema_AContextWrittenBeforeThisPhase_StillValidates()
    {
        ContextSchemaFile.Validate(PreviousShape).Should().BeEmpty(
            "a reading is deprecated, not removed — the root refuses what it does not "
            + "declare, so deleting a property would refuse every existing context");

        ContextSchemaFile.ValidateFile(ContextSchemaFile.ContextPath).Should().BeEmpty(
            "this repository's own context carries most of the readings and is not rewritten");
    }

    [Fact]
    public void Schema_ADeprecatedFieldIsStillAccepted()
    {
        var readings = Fields().Where(field => Marker(field.Node, "READING")).ToList();

        readings.Should().HaveCountGreaterThan(10, "the readings are the point of the phase");
        readings.Should().OnlyContain(field => Marker(field.Node, "DEPRECATED"),
            "a reading says in the schema that it is deprecated, so a reader of the schema "
            + "learns it without reading a phase spec");
        ContextSchemaFile.Validate(PreviousShape).Should().BeEmpty(
            "deprecated means no longer written — never refused");
    }

    [Fact]
    public void Schema_The167cVocabularyPointers_AreUnchanged()
    {
        // ContextSingleValueNormaliser learns "this is an array" from the schema, so deleting
        // a node would silently disable the single-value shorthand the shipped prompt writes.
        Marked("DESCRIPTIVE").Should().HaveCount(6, "the six opened vocabularies are 167c's");
        Marked("DECIDED").Should().HaveCount(4, "the four closed vocabularies are 167c's");

        var normalised = ContextGates.Normaliser().Normalise(JsonNode.Parse(
            """{ "meta": { "workdir": ".", "type": "agent" }, "arch": { "style": "Layered" } }"""));

        normalised!["arch"]!["style"]!.AsArray().Should().HaveCount(1,
            "a deprecated node is still a node — the shorthand still reads it as a list");
    }

    [Fact]
    public void Summary_TheOrchestratorFields_AreUnchanged()
    {
        var summary = new ContextYamlSerializer(new ContextYamlBuilders()).Parse(PreviousShape).Summary!;

        summary.Workdir.Should().Be(".");
        summary.Language.Should().Be("C#");
        summary.Image.Should().Be("mcr.microsoft.com/dotnet/sdk:8.0");
        summary.Prerequisites.Should().Be("npm ci");
        summary.Purpose.Should().Be("Serves orders to the storefront.");
    }

    [Fact]
    public void Purpose_Absent_IsStillTheOneFieldWorthAsking()
    {
        Field("meta", "purpose").Should().Match<JsonNode>(node => Marker(node, "JUDGEMENT"),
            "what a module is FOR appears in no file, so it cannot be a reading");

        DocumentDescription().Should().Contain("meta.purpose",
            "the tool still ASKS for the one line nobody can derive, having stopped asking "
            + "for the ones the repository already states");
    }

    [Fact]
    public void State_IsUntouched()
    {
        var state = ContextSchemaFile.Root["properties"]!["state"]!;

        Marker(state, "JUDGEMENT").Should().BeTrue(
            "every entry records a decision and what came of it");
        Marker(state, "DEPRECATED").Should().BeFalse(
            "the chronicle is the most valuable thing in the file");
    }

    private static string DocumentDescription() =>
        typeof(WriteContextYamlToolHost)
            .GetMethod(nameof(WriteContextYamlToolHost.WriteContextYaml))!
            .GetParameters().Single(p => p.Name == "document")
            .GetCustomAttribute<System.ComponentModel.DescriptionAttribute>()!.Description;

    private static JsonNode Field(string block, string name) =>
        ContextSchemaFile.Root["properties"]![block]!["properties"]![name]!;

    private static bool Marker(JsonNode node, string word) =>
        node["$comment"]?.GetValue<string>().Contains(word, StringComparison.Ordinal) == true;

    // Derived, never listed: the root's own fields (the five container blocks excepted)
    // plus the children of those blocks. A new property joins the rule the moment it exists.
    private static IEnumerable<(string Pointer, JsonNode Node)> Fields()
    {
        foreach (var (name, node) in ContextSchemaFile.Root["properties"]!.AsObject())
        {
            if (Blocks.Contains(name))
                foreach (var (child, childNode) in node!["properties"]!.AsObject())
                    yield return ($"/{name}/{child}", childNode!);
            else
                yield return ($"/{name}", node!);
        }
    }

    private static List<string> Marked(string word) =>
        [.. Walk(ContextSchemaFile.Root, string.Empty)
            .Where(entry => Marker(entry.Node, word))
            .Select(entry => entry.Pointer)];

    private static IEnumerable<(string Pointer, JsonNode Node)> Walk(JsonNode node, string pointer)
    {
        if (node is JsonObject obj)
        {
            yield return (pointer, obj);
            foreach (var (key, child) in obj)
                if (child is not null && key != "examples")
                    foreach (var found in Walk(child, $"{pointer}/{key}")) yield return found;
        }
        else if (node is JsonArray array)
        {
            for (var index = 0; index < array.Count; index++)
                if (array[index] is { } child)
                    foreach (var found in Walk(child, $"{pointer}/{index}")) yield return found;
        }
    }
}
