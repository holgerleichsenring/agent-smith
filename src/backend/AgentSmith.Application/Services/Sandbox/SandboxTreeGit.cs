using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// The git primitives the sandbox tree sync is built from: the tree a sandbox holds, the
/// post-image of every path it changed, and a change carried between two clones of one
/// repository byte for byte.
/// <para>
/// A change leaves its sandbox as a binary diff written to a file first, so git's own exit
/// code decides whether it was taken, and is read back through base64: a step's stdout is
/// assembled line by line and would lose every carriage return of a CRLF file. It arrives
/// through <see cref="ISandboxBinaryFileWriter"/>, decoded here, never by a shell.
/// </para>
/// Every failure throws <see cref="InvalidOperationException"/> naming the sandbox key.
/// </summary>
public sealed class SandboxTreeGit(ISandboxBinaryFileWriter bytes, SandboxGitIdentity identity)
{
    /// <summary>The stdout capture's ceiling; an encoded change reaching it was cut off.</summary>
    public const int EncodedLimit = 1_000_000;
    private const int GitTimeoutSeconds = 120;
    private const string OutPath = ".git/agentsmith-sync.out";
    private const string PatchPath = ".git/agentsmith-sync.patch";
    private const string DiffThenEncode = "out=$1; shift; git diff --binary \"$@\" > \"$out\" && base64 < \"$out\"";

    /// <summary>Stages everything and returns the tree the index now holds.</summary>
    public async Task<string> StageAndWriteTreeAsync(string key, ISandbox sandbox, CancellationToken ct)
    {
        await GitAsync(key, sandbox, ["add", "-A"], ct);
        return (await GitAsync(key, sandbox, ["write-tree"], ct)).Trim();
    }

    public async Task<string> HeadTreeAsync(string key, ISandbox sandbox, CancellationToken ct) =>
        (await GitAsync(key, sandbox, ["rev-parse", "HEAD^{tree}"], ct)).Trim();

    public async Task<bool> HasObjectAsync(ISandbox sandbox, string sha, CancellationToken ct) =>
        (await RunAsync(sandbox, "git", ["cat-file", "-e", sha], ct)).ExitCode == 0;

    public async Task<bool> IndexDiffersFromHeadAsync(ISandbox sandbox, CancellationToken ct) =>
        (await RunAsync(sandbox, "git", ["diff", "--cached", "--quiet"], ct)).ExitCode != 0;

    /// <summary>Every path changed between two trees, with its post-image: "mode blob", or "deleted".</summary>
    public async Task<IReadOnlyDictionary<string, string>> PostImagesAsync(
        string key, ISandbox sandbox, string from, string to, CancellationToken ct)
    {
        var raw = await GitAsync(key, sandbox,
            ["-c", "core.quotePath=false", "diff", "--raw", "--no-abbrev", "--no-renames", "-z", from, to], ct);
        if (raw.Length >= EncodedLimit)
            throw new InvalidOperationException($"{key}: the list of changed paths reached the {EncodedLimit}-character output limit");
        var images = new Dictionary<string, string>(StringComparer.Ordinal);
        var fields = raw.TrimEnd('\n').Split('\0');
        for (var i = 0; i + 1 < fields.Length; i += 2)
        {
            var meta = fields[i].TrimStart(':').Split(' ');
            if (meta.Length < 5) continue;
            images[fields[i + 1]] = meta[4].StartsWith('D') ? "deleted" : $"{meta[1]} {meta[3]}";
        }
        return images;
    }

    /// <summary>The change from one tree to another without <paramref name="excluded"/>, base64-encoded;
    /// empty when there is none, null when the encoded text reached <see cref="EncodedLimit"/>.</summary>
    public async Task<string?> EncodedDiffAsync(
        string key, ISandbox sandbox, string from, string to, IReadOnlyList<string> excluded, CancellationToken ct)
    {
        var result = await RunAsync(sandbox, "sh",
            ["-c", DiffThenEncode, "sh", OutPath, from, to, "--", ".", .. excluded.Select(p => $":(exclude,literal){p}")], ct);
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"{key}: git diff --binary failed (exit {result.ExitCode}): {result.ErrorMessage}");
        var text = result.OutputContent ?? string.Empty;
        return text.Length >= EncodedLimit ? null : text.Trim();
    }

    /// <summary>Applies an encoded change to the index and working tree; null on success, else git's reason.</summary>
    public async Task<string?> ApplyAsync(ISandbox sandbox, string encoded, bool checkOnly, CancellationToken ct)
    {
        if (await bytes.WriteAsync(sandbox, Repository.SandboxWorkPath, PatchPath,
                Convert.FromBase64String(encoded), ct) is { } failed)
            return failed;
        string[] args = checkOnly
            ? ["apply", "--check", "--index", "--whitespace=nowarn", PatchPath]
            : ["apply", "--index", "--whitespace=nowarn", PatchPath];
        var result = await RunAsync(sandbox, "git", args, ct);
        return result.ExitCode == 0 ? null : $"git apply exited {result.ExitCode}: {result.ErrorMessage}";
    }

    /// <summary>Commits the index locally, never pushed; nothing when the index equals HEAD.</summary>
    public async Task SyncCommitAsync(string key, ISandbox sandbox, string message, CancellationToken ct)
    {
        if (!await IndexDiffersFromHeadAsync(sandbox, ct)) return;
        await identity.EnsureConfiguredAsync(sandbox, ct);
        await GitAsync(key, sandbox, ["commit", "--no-verify", "-q", "-m", message], ct);
    }

    private async Task<string> GitAsync(string key, ISandbox sandbox, IReadOnlyList<string> args, CancellationToken ct)
    {
        var result = await RunAsync(sandbox, "git", args, ct);
        if (result.ExitCode != 0)
            throw new InvalidOperationException(
                $"{key}: git {string.Join(' ', args)} failed (exit {result.ExitCode}): {result.ErrorMessage}");
        return result.OutputContent ?? string.Empty;
    }

    private static Task<StepResult> RunAsync(
        ISandbox sandbox, string command, IReadOnlyList<string> args, CancellationToken ct) =>
        sandbox.RunStepAsync(
            new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run, Command: command, Args: args,
                WorkingDirectory: Repository.SandboxWorkPath, TimeoutSeconds: GitTimeoutSeconds),
            progress: null, ct);
}
