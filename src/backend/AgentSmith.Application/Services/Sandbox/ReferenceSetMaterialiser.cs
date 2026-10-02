using System.Security.Cryptography;
using System.Text;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-10-01-283dc: fills a sandbox with one uploaded website, at each file's relative path
/// under the work root, and says which content it holds.
/// <para>
/// Text is written as it is; every other file is written as base64 beside its path and decoded
/// by ONE python3 step over the work root — a constant script, the root as its argument, no path
/// interpolated into any command — because a five-hundred-file set decoded file by file would be
/// five hundred round trips. A marker carries the content hash, so a held sandbox that already
/// holds the set is taken back without a single write.
/// </para>
/// <para>
/// 2026-10-02-075da: TEXT IS WHAT DECODES. A set holds any type now, so the rule is no extension
/// list but the bytes: strict UTF-8 and no NUL. The encoded suffix is one no upload carries, so an
/// operator's own <c>*.b64</c> file is never decoded.
/// </para>
/// </summary>
public sealed class ReferenceSetMaterialiser(IReferenceSetReader sets, ISandboxFileReaderFactory files)
{
    internal const string Marker = ".agentsmith-reference";
    internal const string EncodedSuffix = ".agentsmith-b64";
    private const string WorkRoot = "/work";
    private static readonly UTF8Encoding StrictUtf8 = new(false, throwOnInvalidBytes: true);

    internal const string DecodeScript =
        "import base64,os,sys\n"
        + "for d,_,names in os.walk(sys.argv[1]):\n"
        + "    for n in names:\n"
        + "        if n.endswith('.agentsmith-b64'):\n"
        + "            p=os.path.join(d,n)\n"
        + "            open(p[:-15],'wb').write(base64.b64decode(open(p,'rb').read()))\n"
        + "            os.remove(p)\n";

    /// <summary>The set in the sandbox, and its content hash.</summary>
    public Task<string> PrepareAsync(ISandbox sandbox, string sessionId, string setId, CancellationToken ct) =>
        PrepareUnderAsync(sandbox, sessionId, setId, string.Empty, ct);

    /// <summary>
    /// 2026-10-01-283de: the same, under <paramref name="root"/> relative to the work root — the
    /// browser sandbox keeps each set in a directory of its own, and its marker beside it, so a
    /// set is copied once per sandbox life however many renders read it.
    /// </summary>
    public async Task<string> PrepareUnderAsync(
        ISandbox sandbox, string sessionId, string setId, string root, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sandbox);
        var io = files.Create(sandbox);
        if (await io.TryReadAsync(Under(root, Marker), ct) is { Length: > 0 } held) return held.Trim();
        return await WriteUnderAsync(sandbox, await sets.FilesAsync(sessionId, setId, ct), root, ct);
    }

    /// <summary>
    /// 2026-10-01-283df: <paramref name="set"/>'s files under <paramref name="root"/>, already read —
    /// a run reads every cited set before it writes any, so a set it cannot read writes nothing.
    /// </summary>
    public async Task<string> WriteUnderAsync(
        ISandbox sandbox, IReadOnlyList<ReferenceSetFile> set, string root, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sandbox);
        ArgumentNullException.ThrowIfNull(set);
        var io = files.Create(sandbox);
        var encoded = 0;
        foreach (var file in set)
            encoded += await WriteAsync(io, Under(root, file.Path), file, ct) ? 1 : 0;
        if (encoded > 0) await DecodeAsync(sandbox, root.Length == 0 ? WorkRoot : $"{WorkRoot}/{root}", ct);
        var hash = HashOf(set);
        await io.WriteAsync(Under(root, Marker), hash, ct);
        return hash;
    }

    private static string Under(string root, string path) => root.Length == 0 ? path : $"{root}/{path}";

    // True when the file went in encoded and waits for the decode step.
    private static async Task<bool> WriteAsync(ISandboxFileReader io, string path, ReferenceSetFile file, CancellationToken ct)
    {
        if (AsText(file) is { } text)
        {
            await io.WriteAsync(path, text, ct);
            return false;
        }
        await io.WriteAsync(path + EncodedSuffix, Convert.ToBase64String(file.Content), ct);
        return true;
    }

    private static string? AsText(ReferenceSetFile file)
    {
        if (Array.IndexOf(file.Content, (byte)0) >= 0) return null;
        try { return StrictUtf8.GetString(file.Content); }
        catch (DecoderFallbackException) { return null; }
    }

    private static async Task DecodeAsync(ISandbox sandbox, string root, CancellationToken ct)
    {
        var step = new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "python3", Args: ["-c", DecodeScript, root], WorkingDirectory: WorkRoot, TimeoutSeconds: 300);
        var result = await sandbox.RunStepAsync(step, null, ct);
        if (result.ExitCode != 0)
            throw new IOException($"Decoding the upload's binary files failed: {result.ErrorMessage ?? "exit " + result.ExitCode}");
    }

    private static string HashOf(IReadOnlyList<ReferenceSetFile> set)
    {
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var file in set.OrderBy(f => f.Path, StringComparer.Ordinal))
        {
            sha.AppendData(Encoding.UTF8.GetBytes(file.Path + "\0" + file.Content.Length + "\0"));
            sha.AppendData(file.Content);
        }
        return Convert.ToHexStringLower(sha.GetHashAndReset());
    }
}
