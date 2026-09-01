using System.Text.Json.Nodes;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Json.Schema;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// 2026-08-26-167c: the descriptive vocabularies SUGGEST; the decided ones still decide.
/// <para>
/// A list of seventeen architecture styles cannot keep up with the architectures that
/// exist, and a model refused for naming one honestly spends its refusal budget being
/// told to lie. But the open constraint had to accept the corpus that already exists —
/// a lowercase-hyphen slug rule refuses 17 of 17 arch styles, 36 of 36 patterns and
/// this repository's own <c>CleanArch</c> / <c>Command/Handler</c>. So the rule is a
/// bounded free value: two to thirty characters, the punctuation those names already
/// use, and the lazy placeholders refused BY NAME — because length is what refuses a
/// paragraph and a separator refuses none of it (<c>todo</c>, <c>tbd</c> and
/// <c>pipeline-oriented-library-and-cli</c> all satisfy a slug rule).
/// </para>
/// </summary>
public sealed class ContextVocabularyTests
{
    /// <summary>Descriptive: what the thing IS. No list keeps up, so they are open.</summary>
    private static readonly string[] Opened =
    [
        "/properties/meta/properties/type/items",
        "/properties/arch/properties/style/items",
        "/properties/arch/properties/patterns/items",
        "/properties/behavior/properties/api/properties/style",
        "/properties/behavior/properties/ui/properties/type",
        "/properties/quality/properties/testing/properties/style",
    ];

    /// <summary>Decided: a policy or a chosen strategy with an exhaustive state space.</summary>
    private static readonly string[] Closed =
    [
        "/properties/behavior/properties/pipeline/additionalProperties/properties/error",
        "/properties/data/additionalProperties/properties/migrations",
        "/properties/integrations/additionalProperties/properties/type",
        "/properties/quality/properties/lang",
    ];

    [Fact]
    public void Schema_ThisRepositorysOwnContext_StillValidates() =>
        ContextSchemaFile.ValidateFile(ContextSchemaFile.ContextPath).Should().BeEmpty(
            "the open constraint is justified by comparability across the corpus — a rule "
            + "that refuses the corpus destroys the thing it was opened for");

    [Fact]
    public void Schema_EverySuggestedValue_ValidatesUnderTheOpenConstraint()
    {
        // The invariant an earlier draft broke without noticing: moving the known values
        // from `enum` to `examples` is only safe while every one of them still validates.
        var offenders = new List<string>();
        foreach (var (pointer, node) in NodesWithExamples())
        {
            var subschema = JsonSchema.FromText(node.ToJsonString());
            foreach (var example in node["examples"]!.AsArray())
            {
                var result = subschema.Evaluate(example, Options);
                if (!result.IsValid) offenders.Add($"{pointer}: {example?.ToJsonString()}");
            }
        }

        offenders.Should().BeEmpty(
            "a suggestion the schema would refuse is worse than no suggestion — it tells "
            + "the model to write something the write path rejects.\n  "
            + string.Join("\n  ", offenders));
    }

    [Fact]
    public void Schema_EveryVocabulary_IsClassifiedByPointer()
    {
        // "The descriptive ones" left four vocabularies unclassified, and the live
        // document was refused on one of them. Every one is now named.
        var opened = Pointers(node => Marker(node, "DESCRIPTIVE"));
        var closed = Pointers(node => node["enum"] is not null);

        opened.Should().BeEquivalentTo(Opened, "an opened vocabulary is named, not inferred");
        closed.Should().BeEquivalentTo(Closed, "every remaining enum is a deliberate decision");
        closed.Should().OnlyContain(pointer => Marker(Resolve(pointer)!, "DECIDED"),
            "a closed vocabulary says why it is closed, where the next reader will look");
    }

    /// <summary>
    /// 2026-08-31-26d4: a stage has a label, a command and an optional path. The fourth
    /// field is always the one that invents a taxonomy — a kind or a category would have
    /// to be a closed list here, which is precisely the estate vocabulary this schema must
    /// not carry. What a stage IS follows from its command.
    /// </summary>
    [Fact]
    public void Schema_TheVerifyBlock_AddsNoClosedVocabulary()
    {
        var stage = Resolve("/properties/verify/items")!;

        stage["properties"]!.AsObject().Select(pair => pair.Key)
            .Should().BeEquivalentTo(["label", "command", "when_present"]);
        Walk(Resolve("/properties/verify")!, string.Empty)
            .Where(entry => entry.Node["enum"] is not null)
            .Should().BeEmpty("an enumerated field would be a closed list of somebody else's words");
        Pointers(node => node["enum"] is not null).Should().BeEquivalentTo(Closed,
            "the closed vocabularies are still the four 167c named");
    }

    [Theory]
    [InlineData("/properties/quality/properties/lang", "english-only")]
    [InlineData("/properties/data/additionalProperties/properties/migrations", "code-first")]
    public void Schema_QualityLangAndDataMigrations_StayClosed(string pointer, string member)
    {
        var node = Resolve(pointer)!;

        node["enum"]!.AsArray().Select(value => value!.GetValue<string>())
            .Should().Contain(member);
        node["pattern"].Should().BeNull(
            "a policy and a chosen strategy are decisions — opening them would invite a "
            + "value the runtime cannot act on");
    }

    [Theory]
    [InlineData("CleanArch", "Command/Handler")]
    [InlineData("Layered", "Repository")]
    public void Schema_AKnownArchStyleAndPattern_StillValidate(string style, string pattern) =>
        Defect(Arch(style, pattern)).Should().BeNull(
            "the values that were the enum are the values the corpus is written in");

    [Theory]
    [InlineData("Actor-Model", "Medallion")]
    [InlineData("Event-Driven Microservices", "Dependency Injection")]
    public void Schema_AnUnknownButHonestValue_Validates(string style, string pattern) =>
        Defect(Arch(style, pattern)).Should().BeNull(
            "a component the list does not cover is described honestly, not squeezed into "
            + "the nearest wrong word");

    [Theory]
    [InlineData("Port/Adapter")]
    [InlineData("Pub/Sub")]
    [InlineData("Quartz.NET")]
    [InlineData("C# Source-Generators")]
    [InlineData("A+B Split")]
    public void Schema_AValueWithPunctuationTheCorpusUses_Validates(string pattern) =>
        Defect(Arch("Layered", pattern)).Should().BeNull(
            "the corpus already writes '#', '+', '.', '/', '-' and spaces — a slug rule "
            + "would refuse most of what an honest writer types");

    [Fact]
    public void Schema_AnEmptyValue_IsRefused() =>
        Defect(Arch("Layered", "")).Should().Contain("/arch/patterns/0",
            "an empty value is not an answer");

    [Theory]
    [InlineData("todo")]
    [InlineData("TBD")]
    [InlineData("unknown")]
    [InlineData("n/a")]
    [InlineData("???")]
    public void Schema_ALazyPlaceholder_IsRefused(string lazy) =>
        Defect(Arch("Layered", lazy)).Should().Contain("/arch/patterns/0",
            "a slug rule blocks none of the laziness it names, so the placeholders are "
            + "refused by name");

    [Fact]
    public void Schema_AParagraphWithHyphensInsteadOfSpaces_IsRefused() =>
        Defect(Arch("Layered", "pipeline-oriented-library-and-cli")).Should()
            .Contain("/arch/patterns/0")
            .And.Contain("30 characters",
                "length is what refuses a paragraph, whatever it is spelled with");

    private static readonly EvaluationOptions Options = new() { OutputFormat = OutputFormat.List };

    private static string Arch(string style, string pattern) => $$"""
        {
          "meta": { "workdir": "." },
          "stack": { "lang": "C#", "image": "mcr.microsoft.com/dotnet/sdk:8.0" },
          "arch": { "style": [{{Json(style)}}], "patterns": [{{Json(pattern)}}] }
        }
        """;

    private static string Json(string value) => System.Text.Json.JsonSerializer.Serialize(value);

    private static string? Defect(string json) => ContextGates.Rule().Defect(JsonNode.Parse(json));

    private static JsonNode? Resolve(string pointer)
    {
        JsonNode? node = ContextSchemaFile.Root;
        foreach (var segment in pointer.Split('/', StringSplitOptions.RemoveEmptyEntries))
            node = node?[segment];
        return node;
    }

    private static bool Marker(JsonNode node, string word) =>
        node["$comment"]?.GetValue<string>().Contains(word, StringComparison.Ordinal) == true;

    private static List<string> Pointers(Func<JsonObject, bool> predicate) =>
        [.. Walk(ContextSchemaFile.Root!, string.Empty)
            .Where(entry => predicate(entry.Node))
            .Select(entry => entry.Pointer)];

    private static List<(string Pointer, JsonObject Node)> NodesWithExamples() =>
        [.. Walk(ContextSchemaFile.Root!, string.Empty)
            .Where(entry => entry.Node["examples"] is JsonArray)];

    // "examples" is annotation, not schema: recursing into it would judge an example
    // value as if it were a subschema.
    private static IEnumerable<(string Pointer, JsonObject Node)> Walk(JsonNode node, string pointer)
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
