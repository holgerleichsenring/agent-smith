namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-10-09-86e1: how one stored file is shown — <c>text</c> with its first part, <c>image</c>
/// (fetched from the content route), or <c>binary</c> (offered as a download).
/// </summary>
/// <param name="Text">The file's text, cut at the preview bound; null unless the kind is text.</param>
/// <param name="Truncated">Whether <paramref name="Text"/> is only the first part of the file.</param>
public sealed record ReferenceFilePreviewView(string Path, string Kind, long Bytes, string? Text, bool Truncated);
