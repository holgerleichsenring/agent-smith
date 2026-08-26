using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AgentSmith.Tests.Prompts;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// 2026-08-26-167c: the schema accepts the SHAPES the shipped prompt actually writes.
/// <para>
/// The project-bootstrap master hands the model a worked example whose fields are the
/// wrong type — <c>"type": "Angular SPA"</c> and <c>"style": "Layered"</c> where the
/// schema declares arrays, <c>"naming": "..."</c> where it declares a map. Three of
/// the four defects a live run hit were shape, not vocabulary. The master reaches this
/// build as a SHA256-verified tarball, so correcting it is a release there and a pin
/// bump here — it cannot be the condition for a run completing. The schema is the side
/// that gives, and a single value read as a list of one is the reading a person would
/// give it anyway.
/// </para>
/// </summary>
public sealed partial class ContextShorthandTests
{
    [Fact]
    public void Schema_ASingleValueWhereAListIsDeclared_Validates()
    {
        var gate = ContextGates.Build();
        var document = Element("""
            {
              "meta": { "workdir": ".", "type": "agent" },
              "stack": { "lang": "C#", "image": "node:20-bookworm", "infra": "Docker" },
              "arch": { "style": "Layered", "patterns": "Repository" }
            }
            """);

        gate.TryRead(document, out var typed, out var readDefect).Should().BeTrue(readDefect);
        gate.Defect(typed!).Should().BeNull(
            "'Layered' was an INSTRUCTION — the same master says to default to it — so a "
            + "refusal here misplaces the defect onto the writer");
    }

    [Fact]
    public void Schema_ASingleValue_NormalisesToAListOfOne()
    {
        var normalised = ContextGates.Normaliser().Normalise(JsonNode.Parse("""
            { "meta": { "workdir": ".", "type": "agent" }, "arch": { "style": "Layered" } }
            """));

        normalised!["meta"]!["type"]!.AsArray().Select(v => v!.GetValue<string>())
            .Should().Equal(["agent"]);
        normalised["arch"]!["style"]!.AsArray().Select(v => v!.GetValue<string>())
            .Should().Equal(["Layered"], "the YAML on disk carries a list, not a scalar");
    }

    [Fact]
    public void Schema_ASingleValueOfTheWrongShape_NamesTheFieldNotTheClrType()
    {
        // Before this, a JSON string against IReadOnlyList<string> threw and handed the
        // model "System.Collections.Generic.IReadOnlyList`1[System.String]" — a name it
        // cannot act on. The list case is now normalised away; the rest names the field.
        var gate = ContextGates.Build();

        gate.TryRead(Element("""{ "meta": { "workdir": 7 } }"""), out _, out var defect)
            .Should().BeFalse();

        defect.Should().Contain("workdir").And.NotContain("System.");
    }

    [Theory]
    [InlineData("""{ "classes": "PascalCase", "fields": "_camelCase" }""")]
    [InlineData("\"Types are PascalCase, fields _camelCase, interfaces I-prefixed.\"")]
    public void Schema_NamingAsASentenceOrAMap_BothValidate(string naming) =>
        ContextGates.Rule().Defect(JsonNode.Parse($$"""
            {
              "meta": { "workdir": "." },
              "stack": { "lang": "C#", "image": "node:20-bookworm" },
              "quality": { "naming": {{naming}} }
            }
            """)).Should().BeNull(
            "the schema demanded a map and the shipped master writes a string — a sentence "
            + "carries the same information to the only audience this field has");

    /// <summary>
    /// The anti-drift assertion for the OTHER repository: a pin bump that reintroduces
    /// the disagreement fails the build here rather than a customer's run.
    /// <para>
    /// The example's angle-bracket slots are filled with defensible values before the
    /// check — they are prompts to the model, not data. What is judged is the SHAPE:
    /// which fields are scalars, which are lists, which is a map.
    /// </para>
    /// </summary>
    [Fact]
    public void Prompt_TheEmbeddedBootstrapExample_ValidatesAgainstTheShippedSchema()
    {
        var example = Materialise(WorkedExample(PackagedMaster.Read("project-bootstrap")));

        var defect = ContextGates.Rule().Defect(
            ContextGates.Normaliser().Normalise(JsonNode.Parse(example)));

        defect.Should().BeNull(
            "the master's worked example is what every bootstrap round is told to copy — "
            + $"a shape the schema refuses is a refusal on every run.\n{example}");
    }

    private static JsonElement Element(string json) => JsonDocument.Parse(json).RootElement;

    private static string WorkedExample(string master)
    {
        var block = JsonFence().Match(master);
        block.Success.Should().BeTrue("the master's context.yaml shape is a ```json block");
        return block.Groups[1].Value;
    }

    // The example is a template, not a document: "<...>" slots are instructions to the
    // model and the behavior line is prose inside braces. Both are filled in, and only
    // meta.version needs a real value — semver is the one slot whose shape the schema
    // constrains beyond the open vocabulary.
    private static string Materialise(string example)
    {
        var filled = Placeholder().Replace(example, "\"Sample\"");
        filled = BehaviorLine().Replace(filled, string.Empty);
        filled = TrailingComma().Replace(filled, "$1");
        var document = JsonNode.Parse(filled)!;
        document["meta"]!["version"] = "1.0.0";
        return document.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    [GeneratedRegex(@"```json\s*(\{.*?\})\s*```", RegexOptions.Singleline)]
    private static partial Regex JsonFence();

    [GeneratedRegex("\"(<[^\"]*>|\\.\\.\\.)\"")]
    private static partial Regex Placeholder();

    [GeneratedRegex(@"\n[^\n]*""behavior""[^\n]*")]
    private static partial Regex BehaviorLine();

    [GeneratedRegex(@",(\s*[}\]])")]
    private static partial Regex TrailingComma();
}
