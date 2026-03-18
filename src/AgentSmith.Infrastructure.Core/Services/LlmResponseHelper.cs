using YamlDotNet.RepresentationModel;

namespace AgentSmith.Infrastructure.Core.Services;

/// <summary>
/// Shared utilities for processing LLM responses.
/// Centralizes code-fence stripping and YAML validation
/// previously duplicated across ContextGenerator and CodeMapGenerator.
/// </summary>
public static class LlmResponseHelper
{
    public static string StripCodeFences(string text)
    {
        text = text.Trim();
        if (text.StartsWith("```yaml", StringComparison.OrdinalIgnoreCase))
            text = text[7..];
        else if (text.StartsWith("```"))
            text = text[3..];
        if (text.EndsWith("```"))
            text = text[..^3];

        return text.Trim();
    }

    public static bool IsValidYaml(string yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml))
            return false;

        try
        {
            var stream = new YamlStream();
            stream.Load(new StringReader(yaml));
            return stream.Documents.Count > 0;
        }
        catch
        {
            return false;
        }
    }
}
