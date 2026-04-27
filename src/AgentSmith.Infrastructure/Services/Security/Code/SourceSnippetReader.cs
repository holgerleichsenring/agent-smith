namespace AgentSmith.Infrastructure.Services.Security.Code;

/// <summary>
/// Reads a bounded line range from a source file. Used by route mappers and
/// extractors to capture handler bodies and config blocks.
/// </summary>
internal static class SourceSnippetReader
{
    public const int DefaultHandlerLines = 30;
    public const int DefaultBlockLines = 50;

    public static (int StartLine, int EndLine, string Content) Read(
        string filePath, int startLine, int linesAfter)
    {
        if (!File.Exists(filePath))
            return (startLine, startLine, string.Empty);

        var allLines = File.ReadAllLines(filePath);
        var safeStart = Math.Max(1, startLine);
        var safeEnd = Math.Min(allLines.Length, safeStart + linesAfter);
        if (safeEnd < safeStart) safeEnd = safeStart;

        var slice = allLines.Skip(safeStart - 1).Take(safeEnd - safeStart + 1);
        return (safeStart, safeEnd, string.Join("\n", slice));
    }

    public static int LineNumberFromOffset(string text, int offset)
    {
        if (offset <= 0) return 1;
        var line = 1;
        for (var i = 0; i < offset && i < text.Length; i++)
            if (text[i] == '\n') line++;
        return line;
    }
}
