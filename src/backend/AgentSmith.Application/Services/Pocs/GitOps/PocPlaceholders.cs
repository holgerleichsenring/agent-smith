using System.Globalization;

namespace AgentSmith.Application.Services.Pocs.GitOps;

/// <summary>2026-11-23-c81eb: the app template's string placeholders — {app} the PoC's name, {namespace} its namespace,
/// {host} the target's domain_pattern with the name, {port} the port of the revision the desired state names. Replaced
/// as text before the file is parsed; the default template quotes each one.</summary>
public static class PocPlaceholders
{
    public static string Fill(string text, PocRenderInput input)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(input);
        return text.Replace("{app}", input.Name, StringComparison.Ordinal)
            .Replace("{namespace}", input.Namespace, StringComparison.Ordinal)
            .Replace("{host}", input.Host, StringComparison.Ordinal)
            .Replace("{port}", input.Port.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }
}
