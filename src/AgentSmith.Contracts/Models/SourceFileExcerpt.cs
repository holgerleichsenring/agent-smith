namespace AgentSmith.Contracts.Models;

/// <summary>
/// A bounded excerpt of a source file with a recorded reason. Carries the
/// file path and line range so findings can attach back to it.
/// </summary>
public sealed record SourceFileExcerpt(
    string File,
    int StartLine,
    int EndLine,
    string Content,
    string Reason);
