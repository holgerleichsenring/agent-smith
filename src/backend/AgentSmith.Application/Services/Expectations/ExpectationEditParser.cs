using AgentSmith.Contracts.Expectations;

namespace AgentSmith.Application.Services.Expectations;

/// <summary>
/// p0328: parses an operator's EDITED expectation text back into the schema —
/// edits are structured data, never free prose appended to the draft. Accepts
/// the canonical <see cref="ExpectationMarkdown"/> form (the shape every
/// transport shows the operator) or a raw JSON object; returns null when the
/// text matches neither, so the caller can fail loudly with guidance.
/// </summary>
public static class ExpectationEditParser
{
    public static ExpectationDraft? TryParse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        return ExpectationDraftParser.TryParse(text) ?? TryParseMarkdown(text);
    }

    private static ExpectationDraft? TryParseMarkdown(string text)
    {
        var sections = SplitSections(text);
        if (!sections.TryGetValue("expected", out var expectedLines)) return null;
        var expected = Bullets(expectedLines);
        if (expected.Count == 0) return null;

        var observed = sections.TryGetValue("observed", out var observedLines)
            ? string.Join(" ", observedLines.Where(l => l.Trim().Length > 0)).Trim()
            : string.Empty;
        var constraints = sections.TryGetValue("constraints", out var constraintLines)
            ? Bullets(constraintLines) : [];
        var question = sections.TryGetValue("open question", out var questionLines)
            ? ParseOpenQuestion(questionLines) : null;
        return new ExpectationDraft(observed, expected, constraints, question);
    }

    private static Dictionary<string, List<string>> SplitSections(string text)
    {
        var sections = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        List<string>? current = null;
        foreach (var line in text.Replace("\r\n", "\n").Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith('#'))
            {
                current = [];
                sections[trimmed.TrimStart('#').Trim()] = current;
            }
            else
            {
                current?.Add(line);
            }
        }
        return sections;
    }

    // Accepts "- [ ] x", "- [x] x", "- x", "* x", "1. x" — operators edit in
    // whatever list style their client produced.
    private static List<string> Bullets(List<string> lines) =>
        lines.Select(StripBullet)
            .Where(item => item.Length > 0)
            .ToList();

    private static string StripBullet(string line)
    {
        var s = line.Trim();
        if (s.StartsWith("- ") || s.StartsWith("* ")) s = s[2..].Trim();
        else if (s.Length > 2 && char.IsDigit(s[0]) && (s[1] == '.' || s[1] == ')')) s = s[2..].Trim();
        else if (!s.StartsWith("[")) return string.Empty;
        if (s.StartsWith("[ ]") || s.StartsWith("[x]", StringComparison.OrdinalIgnoreCase))
            s = s[3..].Trim();
        return s;
    }

    private static ExpectationOpenQuestion? ParseOpenQuestion(List<string> lines)
    {
        string? question = null, optionA = null, optionB = null;
        foreach (var raw in lines.Select(l => StripBulletKeepText(l)).Where(l => l.Length > 0))
        {
            if (raw.StartsWith("A:", StringComparison.OrdinalIgnoreCase)) optionA = raw[2..].Trim();
            else if (raw.StartsWith("B:", StringComparison.OrdinalIgnoreCase)) optionB = raw[2..].Trim();
            else question = question is null ? raw : $"{question} {raw}";
        }
        return question is not null && optionA is not null && optionB is not null
            ? new ExpectationOpenQuestion(question, optionA, optionB)
            : null;
    }

    private static string StripBulletKeepText(string line)
    {
        var s = line.Trim();
        if (s.StartsWith("- ") || s.StartsWith("* ")) s = s[2..].Trim();
        return s;
    }
}
