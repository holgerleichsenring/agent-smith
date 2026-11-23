using System.Text.RegularExpressions;

namespace AgentSmith.Application.Services.Pocs;

/// <summary>
/// 2026-11-23-933db: reads a Dockerfile into parser directives and logical instructions the way the builder does —
/// directives only in the comment lines at the very top, a trailing backslash joining the next line (comment and blank
/// lines inside a continuation skipped; an '# escape=`' directive makes the backtick the joining character), comments
/// dropped — so a rule matches what is built, not how it was wrapped.
/// The final stage is everything from the last FROM on.
/// </summary>
public sealed partial class DockerfileStages
{
    public DockerfileDocument Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var directives = new List<DockerfileInstruction>();
        var n = 0;
        for (; n < lines.Length && Directive().Match(lines[n].Trim()) is { Success: true } d; n++)
            directives.Add(new DockerfileInstruction(n + 1, d.Groups[1].Value.ToUpperInvariant(), d.Groups[2].Value.Trim()));
        var escape = directives.LastOrDefault(d => d.Keyword == "ESCAPE")?.Arguments == "`" ? '`' : '\\';
        var instructions = new List<DockerfileInstruction>();
        while (n < lines.Length)
        {
            var start = n;
            var logical = Joined(lines, ref n, escape);
            if (logical.Length > 0 && !logical.StartsWith('#')) instructions.Add(Instruction(start + 1, logical));
        }
        return new DockerfileDocument(directives, instructions);
    }

    // The logical line starting at lines[n], continuations joined; n moves past it.
    private static string Joined(string[] lines, ref int n, char escape)
    {
        var logical = lines[n++].Trim();
        if (logical.StartsWith('#')) return logical;
        while (logical.EndsWith(escape) && n < lines.Length)
        {
            logical = logical[..^1].TrimEnd();
            var next = lines[n++].Trim();
            if (next.Length == 0 || next.StartsWith('#')) { logical += escape; continue; }
            logical = $"{logical} {next}";
        }
        return logical.EndsWith(escape) ? logical[..^1].TrimEnd() : logical;
    }

    private static DockerfileInstruction Instruction(int line, string logical)
    {
        var space = logical.IndexOfAny([' ', '\t']);
        return space < 0
            ? new DockerfileInstruction(line, logical.ToUpperInvariant(), string.Empty)
            : new DockerfileInstruction(line, logical[..space].ToUpperInvariant(), logical[(space + 1)..].Trim());
    }

    [GeneratedRegex(@"^#\s*([A-Za-z_]+)\s*=\s*(.*)$")]
    private static partial Regex Directive();
}
