using System.Text;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services;

/// <summary>
/// Formats compressed API scan findings (Nuclei, Spectral, ZAP) into
/// compact markdown slices for skill consumption.
/// </summary>
internal static class ApiScanFindingsFormatter
{
    private static readonly HashSet<string> AuthSpectralCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "owasp:api2", "owasp:api5", "oas3-schema-security",
    };

    private static readonly string[] AuthSpectralKeywords =
        ["security", "auth", "bearer", "oauth", "api-key", "apikey", "jwt", "token"];

    private static readonly string[] AuthNucleiKeywords =
        ["auth", "jwt", "token", "session", "cookie", "oauth", "bearer"];

    internal static bool IsAuthSpectral(SpectralFinding f)
    {
        if (AuthSpectralCodes.Contains(f.Code))
            return true;

        var codeLower = f.Code.ToLowerInvariant();
        var messageLower = f.Message.ToLowerInvariant();
        return AuthSpectralKeywords.Any(kw => codeLower.Contains(kw) || messageLower.Contains(kw));
    }

    internal static bool IsAuthNuclei(NucleiFinding f)
    {
        var templateLower = f.TemplateId.ToLowerInvariant();
        var nameLower = f.Name.ToLowerInvariant();
        return AuthNucleiKeywords.Any(kw => templateLower.Contains(kw) || nameLower.Contains(kw));
    }

    internal static string FormatSpectralFindings(string label, List<SpectralFinding> findings)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"### Spectral {label} Findings ({findings.Count})");
        foreach (var f in findings)
            sb.AppendLine($"- [{f.Severity.ToUpperInvariant()}] {f.Code}: {f.Message} — {f.Path}");
        sb.AppendLine();
        return sb.ToString();
    }

    internal static string FormatNucleiFindings(IReadOnlyList<NucleiFinding> findings) =>
        FormatNucleiFindings(null, findings);

    internal static string FormatNucleiFindings(string? label, IReadOnlyList<NucleiFinding> findings)
    {
        var sb = new StringBuilder();
        var heading = label is not null ? $"Nuclei {label}" : "Nuclei";
        sb.AppendLine($"### {heading} Findings ({findings.Count})");
        foreach (var f in findings)
            sb.AppendLine($"- [{f.Severity.ToUpperInvariant()}] {f.TemplateId}: {f.Name} — {f.MatchedUrl}");
        sb.AppendLine();
        return sb.ToString();
    }

    internal static string FormatZapFindings(IReadOnlyList<ZapFinding> findings)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"### ZAP DAST Findings ({findings.Count})");
        foreach (var f in findings)
            sb.AppendLine($"- [{f.RiskDescription.ToUpperInvariant()}] {f.Name} — {f.Url} ({f.Count} instances)");
        sb.AppendLine();
        return sb.ToString();
    }

    internal static void AppendNucleiSummary(StringBuilder sb, NucleiResult nuclei)
    {
        var bySev = nuclei.Findings
            .GroupBy(f => f.Severity, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => SeverityOrder(g.Key));
        sb.AppendLine($"### Nuclei ({nuclei.Findings.Count} findings)");
        foreach (var g in bySev)
            sb.AppendLine($"- {g.Key.ToUpperInvariant()}: {g.Count()}");
        sb.AppendLine();
    }

    internal static void AppendSpectralSummary(StringBuilder sb, SpectralResult spectral)
    {
        sb.AppendLine($"### Spectral ({spectral.Findings.Count} findings — {spectral.ErrorCount} errors, {spectral.WarnCount} warnings)");
        var byCode = spectral.Findings
            .GroupBy(f => f.Code, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count());
        foreach (var g in byCode)
            sb.AppendLine($"- {g.Key}: {g.Count()} ({g.First().Severity})");
        sb.AppendLine();
    }

    internal static void AppendZapSummary(StringBuilder sb, ZapResult zap)
    {
        sb.AppendLine($"### ZAP ({zap.Findings.Count} findings)");
        foreach (var f in zap.Findings)
            sb.AppendLine($"- [{f.RiskDescription}] {f.Name} ({f.Count} instances)");
        sb.AppendLine();
    }

    internal static int SeverityOrder(string severity) => severity.ToLowerInvariant() switch
    {
        "critical" => 4,
        "high" => 3,
        "medium" => 2,
        "low" => 1,
        _ => 0,
    };
}
