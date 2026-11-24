using System.Text;
using AgentSmith.Contracts.Pocs;

namespace AgentSmith.Application.Services.Pocs.Scan;

/// <summary>
/// 2026-11-23-933dc: the explanation prompt of a blocked PoC verdict — each finding's rule, title, detail, file, line,
/// masked snippet and fix, never a file body and never a raw matched value. The rules have already decided; the model
/// only explains to the uploader why it blocked and how to fix it.
/// </summary>
public sealed class PocScanPrompt
{
    public string For(string pocName, PocScanVerdict verdict)
    {
        ArgumentNullException.ThrowIfNull(verdict);
        var prompt = new StringBuilder()
            .AppendLine($"The upload of the PoC '{pocName}' was blocked by deterministic scan rules. The verdict is final; you")
            .AppendLine("cannot change it. Explain to the person who uploaded it, in plain words, what each blocking finding")
            .AppendLine("means and exactly how to fix it, then say what the notes mean in one short paragraph. Values are")
            .AppendLine("masked as <secret at file:line>; never guess or repeat a value.")
            .AppendLine().AppendLine("Blocking findings:");
        foreach (var finding in verdict.Blocks) Append(prompt, finding);
        prompt.AppendLine().AppendLine("Notes:");
        foreach (var finding in verdict.Notes) Append(prompt, finding);
        return prompt.ToString();
    }

    private static void Append(StringBuilder prompt, PocScanFinding f) =>
        prompt.AppendLine($"- [{f.Kind}] {f.RuleId}: {f.Title} at {f.File}{(f.Line is { } line ? $":{line}" : string.Empty)}")
            .AppendLine($"  detail: {f.Detail}")
            .AppendLine(f.MaskedSnippet is { } snippet ? $"  snippet: {snippet}" : "  snippet: (none)")
            .AppendLine(f.Fix is { } fix ? $"  fix: {fix}" : "  fix: (none given)");
}
