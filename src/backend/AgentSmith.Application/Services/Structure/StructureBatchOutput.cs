using System.Text;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Application.Services.Structure;

/// <summary>
/// 2026-10-13-ee83: parses what <see cref="StructureBatchScript"/> printed. Without the closing
/// 'end &lt;count&gt;' for the whole chunk the output may be cut and nothing in it is trusted. Each
/// file is decoded as the container's ReadFile decodes it: over 1 MB or not strict UTF-8 is
/// unreadable, a UTF-8 BOM is stripped. A line that is not base64 is left unanswered.
/// </summary>
public sealed class StructureBatchOutput
{
    private static readonly UTF8Encoding Strict = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public StructureBatchAnswer? Parse(string? output, int count)
    {
        var lines = (output ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0 || lines[^1].TrimEnd() != $"end {count}") return null;
        var read = new Dictionary<int, string?>();
        var skipped = new HashSet<int>();
        foreach (var line in lines[..^1])
            Take(line, count, read, skipped);
        return new StructureBatchAnswer(read, skipped);
    }

    private static void Take(string line, int count, Dictionary<int, string?> read, HashSet<int> skipped)
    {
        var parts = line.Split(' ', 2);
        if (parts[0] == "skip" && Index(parts.ElementAtOrDefault(1), count) is { } skip) skipped.Add(skip);
        else if (Index(parts[0], count) is { } index && TryDecode(parts.ElementAtOrDefault(1)?.Trim() ?? string.Empty, out var content))
            read[index] = content;
    }

    private static int? Index(string? text, int count) =>
        int.TryParse(text, out var index) && index >= 0 && index < count ? index : null;

    // False is "not answered"; true with a null content is "unreadable".
    private static bool TryDecode(string payload, out string? content)
    {
        content = null;
        var bytes = new byte[payload.Length / 4 * 3];
        if (!Convert.TryFromBase64String(payload, bytes, out var length)) return false;
        if (length > SizeLimits.ReadFileMaxBytes) return true;
        var bom = length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
        try { content = Strict.GetString(bytes, bom, length - bom); }
        catch (DecoderFallbackException) { content = null; }
        return true;
    }
}
