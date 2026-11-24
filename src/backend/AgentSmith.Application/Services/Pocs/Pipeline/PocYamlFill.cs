using System.Text;

namespace AgentSmith.Application.Services.Pocs.Pipeline;

/// <summary>
/// 2026-11-23-933dd: fills a pipeline template — a line holding only '@@NAME@@' takes a block (a script, an env list),
/// each of its lines indented as the placeholder was; '@@NAME@@' inside a line takes a scalar. A placeholder left over
/// is a rendering fault, never shipped.
/// </summary>
public sealed class PocYamlFill
{
    public string Fill(string template, IReadOnlyDictionary<string, string> scalars, IReadOnlyDictionary<string, string> blocks)
    {
        ArgumentNullException.ThrowIfNull(template);
        var output = new StringBuilder();
        foreach (var line in template.Split('\n'))
            output.Append(Line(line, scalars, blocks)).Append('\n');
        var text = output.ToString();
        var left = text.IndexOf("@@", StringComparison.Ordinal);
        if (left >= 0) throw new InvalidOperationException($"The pipeline template left a placeholder unfilled: {text[left..Math.Min(text.Length, left + 40)]}");
        return text;
    }

    private static string Line(string line, IReadOnlyDictionary<string, string> scalars, IReadOnlyDictionary<string, string> blocks)
    {
        var trimmed = line.TrimStart();
        if (trimmed.StartsWith("@@", StringComparison.Ordinal) && trimmed.EndsWith("@@", StringComparison.Ordinal)
            && blocks.TryGetValue(trimmed.Trim('@'), out var block))
        {
            var indent = line[..(line.Length - trimmed.Length)];
            return string.Join('\n', block.Split('\n').Select(l => l.Length == 0 ? l : indent + l));
        }
        return scalars.Aggregate(line, (text, s) => text.Replace($"@@{s.Key}@@", s.Value, StringComparison.Ordinal));
    }
}
