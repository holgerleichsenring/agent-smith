using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Structure;

/// <summary>
/// 2026-10-13-ee83: reads the sources of a structure baseline in a few sandbox steps instead of one
/// per file — measured on a container sandbox at about 265 ms per ReadFile step, so a repository of
/// four hundred sources took 106 s one by one. Files a step skipped for its byte budget go to the
/// next step; a step that answered no file, a missing end marker, a non-zero exit or a file without
/// a line is read on its own, so the batch can only be faster than the per-file read, never wrong.
/// A null content is a file the container's ReadFile refuses.
/// </summary>
public sealed class StructureBatchedReader(StructureBatchScript script, StructureBatchOutput output)
{
    public async Task<IReadOnlyDictionary<string, string?>> ReadAsync(
        ISandbox sandbox, ISandboxFileReader files, IReadOnlyList<string> paths, CancellationToken cancellationToken)
    {
        var contents = new Dictionary<string, string?>(StringComparer.Ordinal);
        var pending = new Queue<string>(paths.Distinct(StringComparer.Ordinal));
        var alone = new List<string>();
        while (pending.Count > 0)
        {
            var chunk = Take(pending);
            var (again, single) = await ReadChunkAsync(sandbox, chunk, contents, cancellationToken);
            foreach (var path in again) pending.Enqueue(path);
            alone.AddRange(single);
        }
        foreach (var path in alone)
            contents[path] = await files.TryReadAsync(path, cancellationToken);
        return contents;
    }

    private async Task<(IEnumerable<string> Again, IEnumerable<string> Alone)> ReadChunkAsync(
        ISandbox sandbox, IReadOnlyList<string> chunk, Dictionary<string, string?> contents, CancellationToken ct)
    {
        var result = await sandbox.RunStepAsync(script.For(chunk), null, ct);
        if ((result.ExitCode == 0 ? output.Parse(result.OutputContent, chunk.Count) : null) is not { } answer)
            return ([], chunk);
        foreach (var (index, content) in answer.Read) contents[chunk[index]] = content;
        var skipped = answer.Skipped.Order().Select(i => chunk[i]).ToList();
        var unanswered = Enumerable.Range(0, chunk.Count)
            .Where(i => !answer.Read.ContainsKey(i) && !answer.Skipped.Contains(i)).Select(i => chunk[i]);
        return answer.Read.Count == 0 ? ([], unanswered.Concat(skipped)) : (skipped, unanswered);
    }

    private static List<string> Take(Queue<string> pending)
    {
        var chunk = new List<string>(Math.Min(pending.Count, StructureBatchScript.ChunkSize));
        while (chunk.Count < StructureBatchScript.ChunkSize && pending.TryDequeue(out var path)) chunk.Add(path);
        return chunk;
    }
}
