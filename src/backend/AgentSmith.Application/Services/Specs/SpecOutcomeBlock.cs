using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-06-03c7e: the <c>outcome:</c> block an executed spec carries in <c>specs/done/</c> —
/// the run that executed it, what it delivered and what the phase review still finds. Rendered
/// as YAML, multi-line text as literal blocks, so the done file stays one parseable document.
/// </summary>
public static class SpecOutcomeBlock
{
    public const string Key = "outcome";
    private const string RunKey = "run";
    private const string DeliveredKey = "delivered";
    private const string ReviewKey = "review";

    public static string Render(string? runId, string delivered, string review)
    {
        var outcome = new YamlMappingNode();
        Add(outcome, RunKey, runId);
        Add(outcome, DeliveredKey, delivered);
        Add(outcome, ReviewKey, review);
        var root = new YamlMappingNode { { new YamlScalarNode(Key), outcome } };
        using var writer = new StringWriter();
        new YamlStream(new YamlDocument(root)).Save(writer, assignAnchors: false);
        return Strip(writer.ToString());
    }

    private static void Add(YamlMappingNode map, string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        var text = value.TrimEnd();
        var style = text.Contains('\n') ? ScalarStyle.Literal : ScalarStyle.Any;
        map.Add(new YamlScalarNode(key), new YamlScalarNode(text) { Style = style });
    }

    // YamlStream ends a document with a "..." marker; the block is spliced after the spec.
    private static string Strip(string yaml)
    {
        var text = yaml.TrimEnd();
        if (text.EndsWith("...", StringComparison.Ordinal)) text = text[..^3].TrimEnd();
        return text + "\n";
    }
}
