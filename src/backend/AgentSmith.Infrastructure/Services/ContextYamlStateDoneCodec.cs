using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using YamlDotNet.Core;

namespace AgentSmith.Infrastructure.Services;

/// <summary>
/// 2026-08-26-31e5: splices one phase index line into <c>state.done</c>, newest first,
/// replacing an entry that already carries the same phase id.
/// <para>
/// An upsert, not an append, because a re-run is a DESIGNED path — the parking preset
/// deliberately excludes the record step from the work it skips ahead over — and a second
/// entry under the same key makes the file unparseable rather than merely untidy.
/// </para>
/// </summary>
public sealed class ContextYamlStateDoneCodec(ContextYamlBuilders builders) : IContextYamlStateDoneCodec
{
    public ContextYamlUpsertResult Upsert(string? yaml, string phaseId, string entry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phaseId);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry);
        var text = yaml ?? string.Empty;
        var eol = text.Contains("\r\n", StringComparison.Ordinal) ? "\r" : string.Empty;
        var lines = text.Split('\n').ToList();
        var block = ContextStateBlock.Locate(lines);

        var spliced = block.StateLine == ContextStateBlock.Missing
            ? Append(lines, phaseId, entry, eol)
            : Insert(lines, block, phaseId, entry, eol);
        if (spliced is null)
            return ContextYamlUpsertResult.Error(
                "state.done is written inline; the index line needs a block mapping there.");

        var result = string.Join('\n', spliced);
        return Reparses(result, out var error)
            ? ContextYamlUpsertResult.Ok(result)
            : ContextYamlUpsertResult.Error(error!);
    }

    // A repository that has never had a state section gets one in the shape the schema
    // requires: /state declares done AND active required, and refuses anything else.
    private static List<string> Append(List<string> lines, string phaseId, string entry, string eol)
    {
        var spliced = new List<string>(lines);
        while (spliced.Count > 0 && spliced[^1].Trim().Length == 0) spliced.RemoveAt(spliced.Count - 1);
        spliced.Add(string.Empty + eol);
        spliced.Add("state:" + eol);
        spliced.Add("  done:" + eol);
        spliced.Add($"    {Entry(phaseId, entry)}" + eol);
        spliced.Add("  active: {}" + eol);
        spliced.Add(string.Empty);
        return spliced;
    }

    private static List<string>? Insert(
        List<string> lines, ContextStateBlock block, string phaseId, string entry, string eol)
    {
        var spliced = new List<string>(lines);
        if (ContextStateBlock.InlineValue(spliced[block.StateLine]).Length > 0) return null;

        var done = block.DoneLine;
        if (done == ContextStateBlock.Missing)
        {
            done = block.StateLine + 1;
            spliced.Insert(done, new string(' ', block.DoneIndent) + "done:" + eol);
        }
        else
        {
            var inline = ContextStateBlock.InlineValue(spliced[done]);
            if (inline.Length > 0 && inline != "{}") return null;
            if (inline == "{}") spliced[done] = new string(' ', block.DoneIndent) + "done:" + eol;
            RemoveExisting(spliced, done, block.EntryIndent, phaseId);
        }

        spliced.Insert(done + 1, new string(' ', block.EntryIndent) + Entry(phaseId, entry) + eol);
        return spliced;
    }

    // The id's own entry and every line that continues it — a folded or quoted value can
    // run past its first line, and leaving the tail behind would corrupt the block.
    private static void RemoveExisting(List<string> lines, int done, int entryIndent, string phaseId)
    {
        var end = ContextStateBlock.EndOfBlock(lines, done);
        for (var i = done + 1; i < end; i++)
        {
            if (ContextStateBlock.IsBlank(lines[i])) continue;
            if (ContextStateBlock.IndentOf(lines[i]) != entryIndent) continue;
            if (ContextStateBlock.KeyOf(lines[i]) != phaseId) continue;
            var tail = ContextStateBlock.EndOfBlock(lines, i);
            lines.RemoveRange(i, tail - i);
            return;
        }
    }

    // YAML's double-quoted scalar takes the same two escapes JSON does, and the entry is
    // composed by the writer — one line, no control characters.
    private static string Entry(string phaseId, string entry) =>
        $"{phaseId}: \"{entry.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";

    private bool Reparses(string yaml, out string? error)
    {
        try
        {
            builders.Deserializer.Deserialize<Dictionary<object, object?>>(yaml);
            error = null;
            return true;
        }
        catch (YamlException ex)
        {
            error = $"the spliced context.yaml no longer parses: {ex.Message}";
            return false;
        }
    }
}
