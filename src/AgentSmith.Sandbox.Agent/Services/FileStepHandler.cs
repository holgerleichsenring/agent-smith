using System.Diagnostics;
using System.Text;
using System.Text.Json;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Sandbox.Agent.Services;

internal sealed class FileStepHandler(ILogger<FileStepHandler> logger)
{
    public async Task<StepResult> HandleAsync(
        Step step,
        Func<IReadOnlyList<StepEvent>, Task> onEvents,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            return step.Kind switch
            {
                StepKind.ReadFile => HandleRead(step, sw),
                StepKind.WriteFile => await HandleWriteAsync(step, sw, cancellationToken),
                StepKind.ListFiles => await HandleListAsync(step, onEvents, sw),
                _ => Failure(step, sw, $"Unsupported file kind: {step.Kind}")
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "File step {Kind} failed: {Path}", step.Kind, step.Path);
            return Failure(step, sw, ex.Message);
        }
    }

    private static StepResult HandleRead(Step step, Stopwatch sw)
    {
        var path = step.Path!;
        if (!File.Exists(path))
            return Failure(step, sw, $"file not found: {path}");

        var info = new FileInfo(path);
        if (info.Length > SizeLimits.ReadFileMaxBytes)
            return Failure(step, sw, "file exceeds 1 MB limit");

        var bytes = File.ReadAllBytes(path);
        var bomLen = HasUtf8Bom(bytes) ? 3 : 0;
        try
        {
            var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
            // Decode post-BOM bytes only — keeps the LLM from seeing U+FEFF in
            // its tool output, which it has been observed to mangle into U+0000
            // when echoing the content back via a WriteFile call.
            var content = encoding.GetString(bytes, bomLen, bytes.Length - bomLen);
            return Success(step, sw, content);
        }
        catch (DecoderFallbackException)
        {
            return Failure(step, sw, "binary or non-UTF-8 content not supported");
        }
    }

    private static async Task<StepResult> HandleWriteAsync(Step step, Stopwatch sw, CancellationToken ct)
    {
        var path = step.Path!;
        var content = step.Content!;
        var byteCount = Encoding.UTF8.GetByteCount(content);
        if (byteCount > SizeLimits.WriteFileMaxBytes)
            return Failure(step, sw, "content exceeds 10 MB limit");

        var directory = System.IO.Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        // Mirror the target's BOM convention. Visual Studio writes .cs files
        // with a UTF-8 BOM by default; HandleRead strips it before the LLM
        // sees the content, so we re-emit it on write to keep the file's
        // encoding stable across edits.
        var emitBom = File.Exists(path) && TargetHasUtf8Bom(path);
        var encoding = new UTF8Encoding(emitBom);
        var tempPath = $"{path}.tmp.{Guid.NewGuid():N}";
        await File.WriteAllTextAsync(tempPath, content, encoding, ct);
        File.Move(tempPath, path, overwrite: true);
        return Success(step, sw, null);
    }

    private static bool HasUtf8Bom(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;

    private static bool TargetHasUtf8Bom(string path)
    {
        using var fs = File.OpenRead(path);
        Span<byte> head = stackalloc byte[3];
        return fs.Read(head) == 3 && HasUtf8Bom(head);
    }

    private static async Task<StepResult> HandleListAsync(
        Step step,
        Func<IReadOnlyList<StepEvent>, Task> onEvents,
        Stopwatch sw)
    {
        var path = step.Path!;
        if (!Directory.Exists(path))
            return Failure(step, sw, $"directory not found: {path}");

        var maxDepth = step.MaxDepth ?? 1;
        var entries = new List<string>(SizeLimits.ListFilesMaxEntries);
        var truncated = EnumerateUntilLimit(path, maxDepth, entries);
        if (truncated)
            await onEvents(new[]
            {
                new StepEvent(StepEvent.CurrentSchemaVersion, step.StepId, StepEventKind.Stderr,
                    "directory truncated at 1000 entries", DateTimeOffset.UtcNow)
            });

        var json = JsonSerializer.Serialize(entries, WireFormat.Json);
        return Success(step, sw, json);
    }

    private static bool EnumerateUntilLimit(string root, int maxDepth, List<string> entries)
    {
        var stack = new Stack<(string Path, int Depth)>();
        stack.Push((root, 0));
        while (stack.Count > 0)
        {
            var (current, depth) = stack.Pop();
            foreach (var entry in Directory.EnumerateFileSystemEntries(current))
            {
                if (entries.Count >= SizeLimits.ListFilesMaxEntries) return true;
                entries.Add(entry);
                if (depth + 1 < maxDepth && Directory.Exists(entry))
                    stack.Push((entry, depth + 1));
            }
        }
        return false;
    }

    private static StepResult Success(Step step, Stopwatch sw, string? output) =>
        new(StepResult.CurrentSchemaVersion, step.StepId, ExitCode: 0,
            TimedOut: false, DurationSeconds: sw.Elapsed.TotalSeconds,
            ErrorMessage: null, OutputContent: output);

    private static StepResult Failure(Step step, Stopwatch sw, string message) =>
        new(StepResult.CurrentSchemaVersion, step.StepId, ExitCode: 1,
            TimedOut: false, DurationSeconds: sw.Elapsed.TotalSeconds,
            ErrorMessage: message);
}
