using System.Text;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services;

/// <summary>
/// Compresses raw API scan findings (Nuclei, Spectral, ZAP) into compact summaries
/// and skill-specific category slices. Reduces token usage by routing findings
/// to the skills that need them instead of sending everything to every skill.
/// </summary>
public static class ApiScanFindingsCompressor
{
    private static readonly HashSet<string> AuthSpectralCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "owasp:api2", "owasp:api5", "oas3-schema-security",
    };

    private static readonly string[] AuthSpectralKeywords =
        ["security", "auth", "bearer", "oauth", "api-key", "apikey", "jwt", "token"];

    private static readonly string[] AuthNucleiKeywords =
        ["auth", "jwt", "token", "session", "cookie", "oauth", "bearer"];

    private static readonly Dictionary<string, string[]> SkillCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        ["auth-tester"] = ["auth"],
        ["api-design-auditor"] = ["design"],
        ["dast-analyst"] = ["runtime"],
        ["false-positive-filter"] = ["auth", "design", "runtime"],
        ["dast-false-positive-filter"] = ["runtime"],
        ["api-vuln-analyst"] = ["auth", "design", "runtime"],
    };

    /// <summary>
    /// Builds a compact summary table of all scanner findings.
    /// </summary>
    public static string BuildSummary(
        NucleiResult? nuclei, SpectralResult? spectral, ZapResult? zap)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## API Scan Findings Summary");
        sb.AppendLine();

        if (nuclei is not null && nuclei.Findings.Count > 0)
        {
            var bySev = nuclei.Findings
                .GroupBy(f => f.Severity, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => SeverityOrder(g.Key));
            sb.AppendLine($"### Nuclei ({nuclei.Findings.Count} findings)");
            foreach (var g in bySev)
                sb.AppendLine($"- {g.Key.ToUpperInvariant()}: {g.Count()}");
            sb.AppendLine();
        }

        if (spectral is not null && spectral.Findings.Count > 0)
        {
            sb.AppendLine($"### Spectral ({spectral.Findings.Count} findings — {spectral.ErrorCount} errors, {spectral.WarnCount} warnings)");
            var byCode = spectral.Findings
                .GroupBy(f => f.Code, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => g.Count());
            foreach (var g in byCode)
                sb.AppendLine($"- {g.Key}: {g.Count()} ({g.First().Severity})");
            sb.AppendLine();
        }

        if (zap is not null && zap.Findings.Count > 0)
        {
            sb.AppendLine($"### ZAP ({zap.Findings.Count} findings)");
            foreach (var f in zap.Findings)
                sb.AppendLine($"- [{f.RiskDescription}] {f.Name} ({f.Count} instances)");
            sb.AppendLine();
        }

        if (sb.Length < 50)
            sb.AppendLine("No findings from automated scanners.");

        return sb.ToString();
    }

    /// <summary>
    /// Builds category slices: auth, design, runtime.
    /// </summary>
    public static Dictionary<string, string> BuildCategorySlices(
        NucleiResult? nuclei, SpectralResult? spectral, ZapResult? zap)
    {
        var slices = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var authFindings = new StringBuilder();
        var designFindings = new StringBuilder();
        var runtimeFindings = new StringBuilder();

        // Categorize Spectral findings
        if (spectral is not null && spectral.Findings.Count > 0)
        {
            var authSpectral = new List<SpectralFinding>();
            var designSpectral = new List<SpectralFinding>();

            foreach (var f in spectral.Findings)
            {
                if (IsAuthSpectral(f))
                    authSpectral.Add(f);
                else
                    designSpectral.Add(f);
            }

            if (authSpectral.Count > 0)
            {
                authFindings.AppendLine($"### Spectral Auth Findings ({authSpectral.Count})");
                foreach (var f in authSpectral)
                    authFindings.AppendLine($"- [{f.Severity.ToUpperInvariant()}] {f.Code}: {f.Message} — {f.Path}");
                authFindings.AppendLine();
            }

            if (designSpectral.Count > 0)
            {
                designFindings.AppendLine($"### Spectral Design Findings ({designSpectral.Count})");
                foreach (var f in designSpectral)
                    designFindings.AppendLine($"- [{f.Severity.ToUpperInvariant()}] {f.Code}: {f.Message} — {f.Path}");
                designFindings.AppendLine();
            }
        }

        // Categorize Nuclei findings — all go to runtime, auth-relevant also go to auth
        if (nuclei is not null && nuclei.Findings.Count > 0)
        {
            var authNuclei = new List<NucleiFinding>();

            runtimeFindings.AppendLine($"### Nuclei Findings ({nuclei.Findings.Count})");
            foreach (var f in nuclei.Findings)
            {
                runtimeFindings.AppendLine(
                    $"- [{f.Severity.ToUpperInvariant()}] {f.TemplateId}: {f.Name} — {f.MatchedUrl}");

                if (IsAuthNuclei(f))
                    authNuclei.Add(f);
            }
            runtimeFindings.AppendLine();

            if (authNuclei.Count > 0)
            {
                authFindings.AppendLine($"### Nuclei Auth Findings ({authNuclei.Count})");
                foreach (var f in authNuclei)
                    authFindings.AppendLine(
                        $"- [{f.Severity.ToUpperInvariant()}] {f.TemplateId}: {f.Name} — {f.MatchedUrl}");
                authFindings.AppendLine();
            }
        }

        // ZAP findings go to runtime
        if (zap is not null && zap.Findings.Count > 0)
        {
            runtimeFindings.AppendLine($"### ZAP DAST Findings ({zap.Findings.Count})");
            foreach (var f in zap.Findings)
            {
                runtimeFindings.AppendLine(
                    $"- [{f.RiskDescription.ToUpperInvariant()}] {f.Name} — {f.Url} ({f.Count} instances)");
            }
            runtimeFindings.AppendLine();
        }

        if (authFindings.Length > 0) slices["auth"] = authFindings.ToString();
        if (designFindings.Length > 0) slices["design"] = designFindings.ToString();
        if (runtimeFindings.Length > 0) slices["runtime"] = runtimeFindings.ToString();

        return slices;
    }

    /// <summary>
    /// Returns the relevant finding slice for a specific skill.
    /// Prefers orchestration-declared input_categories; falls back to hardcoded mapping.
    /// </summary>
    public static string GetSliceForSkill(
        string skillName,
        Dictionary<string, string> categorySlices,
        IReadOnlyList<string>? inputCategories = null)
    {
        var categories = inputCategories is { Count: > 0 }
            ? inputCategories
            : SkillCategories.TryGetValue(skillName, out var legacy) ? legacy : null;

        if (categories is null)
            return string.Empty;

        var sb = new StringBuilder();
        foreach (var cat in categories)
        {
            if (categorySlices.TryGetValue(cat, out var slice))
            {
                sb.AppendLine(slice);
            }
        }

        return sb.ToString();
    }

    private static bool IsAuthSpectral(SpectralFinding f)
    {
        if (AuthSpectralCodes.Contains(f.Code))
            return true;

        var codeLower = f.Code.ToLowerInvariant();
        var messageLower = f.Message.ToLowerInvariant();
        return AuthSpectralKeywords.Any(kw => codeLower.Contains(kw) || messageLower.Contains(kw));
    }

    private static bool IsAuthNuclei(NucleiFinding f)
    {
        var templateLower = f.TemplateId.ToLowerInvariant();
        var nameLower = f.Name.ToLowerInvariant();
        return AuthNucleiKeywords.Any(kw => templateLower.Contains(kw) || nameLower.Contains(kw));
    }

    private static int SeverityOrder(string severity) => severity.ToLowerInvariant() switch
    {
        "critical" => 4,
        "high" => 3,
        "medium" => 2,
        "low" => 1,
        _ => 0,
    };
}
