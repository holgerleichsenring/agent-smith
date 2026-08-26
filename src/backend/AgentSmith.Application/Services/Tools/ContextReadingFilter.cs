using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-08-26-04b6: drops the READING keys out of the freeform blocks before the document is
/// judged or written.
/// <para>
/// A reading is a value copied out of a file that is still in the repository: it is worthless
/// on the day it is written and wrong soon after, and nothing can tell which of the two copies
/// is true. `arch.style` and `arch.patterns` are the same defect wearing a taxonomy — a label
/// an agent picks by skimming the tree once, which nobody decided; `quality.principles` was,
/// measured in a live context, every build-file flag read back out, while the authored
/// coding-principles.md is where the intent already lives.
/// </para>
/// <para>
/// The prompt that asks for these fields ships in the skills tarball behind a release and a
/// pin, so the tool DISCARDS them instead of refusing them: a model that still offers one is
/// not punished, and the file simply does not carry it. The typed record drops the rest by
/// having no property to deserialise into.
/// </para>
/// </summary>
public sealed class ContextReadingFilter
{
    private static readonly string[] ArchReadings = ["style", "patterns", "layers", "bounded-contexts"];

    private static readonly string[] QualityReadings = ["principles"];

    public ContextYamlDocument Strip(ContextYamlDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return document with
        {
            Arch = Without(document.Arch, ArchReadings),
            Quality = Without(document.Quality, QualityReadings),
        };
    }

    // An emptied block is dropped whole: an empty map is not a block, it is null, and the
    // schema refuses one.
    private static IDictionary<string, object?>? Without(
        IDictionary<string, object?>? block, string[] readings)
    {
        if (block is null) return null;
        var kept = block
            .Where(entry => !readings.Contains(entry.Key, StringComparer.OrdinalIgnoreCase))
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        return kept.Count > 0 ? kept : null;
    }
}
