using System.Globalization;
using System.Text.Json.Nodes;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace AgentSmith.Application.Services.Validation;

/// <summary>
/// 2026-08-25-2c7c: reads a YAML document into the JSON node a schema is evaluated
/// against, keeping YAML's core-schema scalar types. The one bridge every draft
/// validator crosses.
/// <para>
/// The obvious bridge — deserialize to <c>object</c>, re-serialize with
/// <c>JsonCompatible()</c> — turns every plain scalar into a string, so a correct
/// <c>class-lines: 120</c> is reported as failing <c>type: integer</c>. A rule that
/// misreads its own input invents failures and hides real ones: every integer and
/// boolean constraint behind that bridge is unenforceable, and the failure mode is a
/// false rejection of correct input, so nobody reports it.
/// </para>
/// <para>
/// Only a PLAIN scalar is typed. A quoted <c>"20"</c> is what the author wrote — a
/// string — and stays one, which is how a genuinely wrong type is still caught.
/// </para>
/// </summary>
internal static class YamlAsJson
{
    /// <summary>Throws <see cref="YamlException"/> when the text is not valid YAML.</summary>
    public static JsonNode? Convert(string yaml)
    {
        var stream = new YamlStream();
        using var reader = new StringReader(yaml);
        stream.Load(reader);
        return stream.Documents.Count == 0 ? null : Node(stream.Documents[0].RootNode);
    }

    private static JsonNode? Node(YamlNode node) => node switch
    {
        YamlMappingNode mapping => Mapping(mapping),
        YamlSequenceNode sequence => new JsonArray([.. sequence.Select(Node)]),
        YamlScalarNode scalar => Scalar(scalar),
        _ => null,
    };

    private static JsonObject Mapping(YamlMappingNode mapping)
    {
        var result = new JsonObject();
        foreach (var pair in mapping.Children)
            result[((YamlScalarNode)pair.Key).Value ?? string.Empty] = Node(pair.Value);
        return result;
    }

    private static JsonNode? Scalar(YamlScalarNode scalar)
    {
        var value = scalar.Value ?? string.Empty;
        if (scalar.Style != ScalarStyle.Plain) return JsonValue.Create(value);
        if (value.Length == 0 || value is "~" or "null" or "Null" or "NULL") return null;
        if (value is "true" or "True" or "TRUE") return JsonValue.Create(true);
        if (value is "false" or "False" or "FALSE") return JsonValue.Create(false);
        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var whole))
            return JsonValue.Create(whole);
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var real)
            ? JsonValue.Create(real)
            : JsonValue.Create(value);
    }
}
