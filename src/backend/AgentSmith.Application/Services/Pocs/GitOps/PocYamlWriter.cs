using YamlDotNet.RepresentationModel;

namespace AgentSmith.Application.Services.Pocs.GitOps;

/// <summary>2026-11-23-c81eb: one stable serialisation of a manifest file — each document saved alone, YamlStream's
/// '...' end marker dropped, documents joined by '---' — so equal input renders equal bytes.</summary>
public static class PocYamlWriter
{
    public static string Write(IEnumerable<YamlMappingNode> documents) =>
        string.Join("---\n", documents.Select(Document));

    private static string Document(YamlMappingNode root)
    {
        using var writer = new StringWriter { NewLine = "\n" };
        new YamlStream(new YamlDocument(root)).Save(writer, assignAnchors: false);
        var text = writer.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd();
        if (text.EndsWith("...", StringComparison.Ordinal)) text = text[..^3].TrimEnd();
        return text + "\n";
    }
}
