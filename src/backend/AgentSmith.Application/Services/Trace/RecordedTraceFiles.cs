using AgentSmith.Contracts.Runs;

namespace AgentSmith.Application.Services.Trace;

/// <summary>
/// p0427: moves a recorded run between the store it was recorded into and a directory of
/// files, which is how a run that failed in production becomes a scenario the test suite
/// replays for good.
/// <para>
/// The file name IS the store key without its prefix — <c>0007.answer</c> — so the export
/// is readable, diffable, and reads back through the same key format that wrote it.
/// </para>
/// </summary>
public static class RecordedTraceFiles
{
    public static async Task SaveAsync(
        RecordedTrace trace, string directory, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(directory);
        foreach (var entry in trace.Entries)
            await File.WriteAllTextAsync(
                Path.Combine(directory, RecordedTraceKey.FileName(entry)),
                entry.Content, cancellationToken);
    }

    public static async Task<RecordedTrace> LoadAsync(
        string directory, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(directory))
            throw new DirectoryNotFoundException($"No recorded run at '{directory}'.");
        var entries = new List<RecordedTraceEntry>();
        foreach (var file in Directory.EnumerateFiles(directory))
        {
            var content = await File.ReadAllTextAsync(file, cancellationToken);
            if (RecordedTraceKey.TryParse(Path.GetFileName(file), content, out var entry))
                entries.Add(entry);
        }
        return RecordedTrace.Of(entries);
    }
}
