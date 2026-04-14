using System.Text;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services;

/// <summary>
/// Compresses raw API scan findings into compact summaries and skill-specific slices.
/// </summary>
public static class ApiScanFindingsCompressor
{
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
            ApiScanFindingsFormatter.AppendNucleiSummary(sb, nuclei);

        if (spectral is not null && spectral.Findings.Count > 0)
            ApiScanFindingsFormatter.AppendSpectralSummary(sb, spectral);

        if (zap is not null && zap.Findings.Count > 0)
            ApiScanFindingsFormatter.AppendZapSummary(sb, zap);

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
        var authParts = new StringBuilder();
        var designParts = new StringBuilder();
        var runtimeParts = new StringBuilder();

        if (spectral is not null && spectral.Findings.Count > 0)
        {
            var authSpectral = new List<SpectralFinding>();
            var designSpectral = new List<SpectralFinding>();

            foreach (var f in spectral.Findings)
            {
                if (ApiScanFindingsFormatter.IsAuthSpectral(f))
                    authSpectral.Add(f);
                else
                    designSpectral.Add(f);
            }

            if (authSpectral.Count > 0)
                authParts.Append(ApiScanFindingsFormatter.FormatSpectralFindings("Auth", authSpectral));
            if (designSpectral.Count > 0)
                designParts.Append(ApiScanFindingsFormatter.FormatSpectralFindings("Design", designSpectral));
        }

        if (nuclei is not null && nuclei.Findings.Count > 0)
        {
            runtimeParts.Append(ApiScanFindingsFormatter.FormatNucleiFindings(nuclei.Findings));
            var authNuclei = nuclei.Findings.Where(ApiScanFindingsFormatter.IsAuthNuclei).ToList();
            if (authNuclei.Count > 0)
                authParts.Append(ApiScanFindingsFormatter.FormatNucleiFindings("Auth", authNuclei));
        }

        if (zap is not null && zap.Findings.Count > 0)
            runtimeParts.Append(ApiScanFindingsFormatter.FormatZapFindings(zap.Findings));

        if (authParts.Length > 0) slices["auth"] = authParts.ToString();
        if (designParts.Length > 0) slices["design"] = designParts.ToString();
        if (runtimeParts.Length > 0) slices["runtime"] = runtimeParts.ToString();

        return slices;
    }

    /// <summary>
    /// Returns the relevant finding slice for a specific skill.
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
}
