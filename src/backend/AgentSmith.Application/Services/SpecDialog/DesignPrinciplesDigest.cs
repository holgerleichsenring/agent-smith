using System.Text;
using System.Text.RegularExpressions;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-11-10-14a3c: a design turn's view of the coding principles. They govern how code is written, which a draft
/// cites but does not apply line by line; conversation 43 carried one 12500-character file twice, once per repository
/// holding it. Each distinct file is shown once: the repositories holding it, its composition and status, its Project
/// Specifics (what the project itself added), and the path that holds the rest.
/// </summary>
public static partial class DesignPrinciplesDigest
{
    public static string Render(IReadOnlyList<ContextDocument> documents)
    {
        ArgumentNullException.ThrowIfNull(documents);
        var sb = new StringBuilder();
        foreach (var file in documents.GroupBy(d => d.Content.Trim(), StringComparer.Ordinal))
        {
            var holders = file.Select(d => d.SandboxKey).Distinct(StringComparer.Ordinal).ToList();
            var path = file.First().Path;
            var marker = Marker().Match(file.Key);
            sb.Append("## ").AppendJoin(", ", holders).Append(" — `").Append(path).AppendLine("`");
            if (marker.Success)
                sb.Append("Composed from ").Append(marker.Groups["composed"].Value).Append(", status ")
                    .Append(marker.Groups["status"].Value).AppendLine(".");
            sb.Append("The full rules are at `").Append(path).Append("` in ")
                .Append(holders.Count == 1 ? "that repository" : "each of them")
                .AppendLine("; read them when a draft decides how code is written.");
            if (Specifics(file.Key) is { Length: > 0 } specifics) sb.AppendLine().AppendLine(specifics);
            sb.AppendLine();
        }
        return sb.ToString().TrimEnd();
    }

    private static string Specifics(string body)
    {
        var start = body.IndexOf("## Project Specifics", StringComparison.Ordinal);
        if (start < 0) return string.Empty;
        var next = body.IndexOf("\n## ", start + 3, StringComparison.Ordinal);
        return "#" + (next < 0 ? body[start..] : body[start..next]).Trim();
    }

    [GeneratedRegex(@"agentsmith:principles\s+composed=(?<composed>\S+)\s+status=(?<status>[\w-]+)")]
    private static partial Regex Marker();
}
