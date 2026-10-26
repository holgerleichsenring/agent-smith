using AgentSmith.Application.Services.Json;
using AgentSmith.Application.Models.Registry;

namespace AgentSmith.Application.Services.Registry;

/// <summary>
/// Parses the stager LLM's terminal response — a JSON object
/// <c>{"files":[{"path":"...","content":"..."}]}</c> — into typed
/// <see cref="StagedAuthFile"/> records, wherever in the answer the object sits
/// (a fence, prose around it). Returns an empty list (never throws) when the model produced no
/// parseable object; the caller then reports the gap loudly rather than crashing.
/// </summary>
public sealed class StagedAuthFileJsonReader
{
    // 2026-10-26-cb25d: the envelope wherever it sits in the answer; an object without "files" is passed over.
    public IReadOnlyList<StagedAuthFile> Read(string? modelText) =>
        JsonAnswerReader.ReadObject<Envelope, IReadOnlyList<StagedAuthFile>>(modelText, Files) ?? [];

    private static IReadOnlyList<StagedAuthFile>? Files(Envelope envelope) =>
        envelope.Files?
            .Where(f => !string.IsNullOrWhiteSpace(f.Path) && f.Content is not null)
            .Select(f => new StagedAuthFile(f.Path!, f.Content!))
            .ToList();

    private sealed record Envelope(IReadOnlyList<FileEntry>? Files);
    private sealed record FileEntry(string? Path, string? Content);
}
