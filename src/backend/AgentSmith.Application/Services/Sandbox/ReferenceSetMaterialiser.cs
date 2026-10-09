using System.Security.Cryptography;
using System.Text;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-10-01-283dc: fills a sandbox with one uploaded website, at each file's relative path
/// under the work root, and says which content it holds.
/// <para>
/// 2026-10-08-e8b9j: TEXT IS WHAT DECODES, AND EVERYTHING ELSE GOES AS BYTES. A file that is strict
/// UTF-8 with no NUL and fits a WriteFile is written as text, which every agent reads; any other
/// goes through <see cref="ISandboxBinaryFileWriter"/> — WriteBytes steps decoded by the receiver
/// in C#. It used to go as base64 decoded by one python3 step, which the in-process backend (the
/// server's own image) does not have, and which a binary over ~7.5 MB never reached because its
/// base64 broke WriteFile's 10 MB bound. A marker carries the content hash, so a held sandbox that
/// already holds the set is taken back without a single write.
/// </para>
/// </summary>
public sealed class ReferenceSetMaterialiser(
    IReferenceSetReader sets, ISandboxFileReaderFactory files, ISandboxBinaryFileWriter bytes)
{
    internal const string Marker = ".agentsmith-reference";
    private static readonly UTF8Encoding StrictUtf8 = new(false, throwOnInvalidBytes: true);

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
        foreach (var file in set) await WriteAsync(sandbox, io, root, file, ct);
        var hash = HashOf(set);
        await io.WriteAsync(Under(root, Marker), hash, ct);
        return hash;
    }

    private static string Under(string root, string path) => root.Length == 0 ? path : $"{root}/{path}";

    private async Task WriteAsync(ISandbox sandbox, ISandboxFileReader io, string root, ReferenceSetFile file, CancellationToken ct)
    {
        if (AsText(file) is { } text)
        {
            await io.WriteAsync(Under(root, file.Path), text, ct);
            return;
        }
        if (await bytes.WriteAsync(sandbox, root, file.Path, file.Content, ct) is { } failed)
            throw new IOException($"Writing the upload's file '{file.Path}' failed: {failed}");
    }

    private static string? AsText(ReferenceSetFile file)
    {
        if (file.Content.LongLength > SizeLimits.WriteFileMaxBytes || Array.IndexOf(file.Content, (byte)0) >= 0) return null;
        try { return StrictUtf8.GetString(file.Content); }
        catch (DecoderFallbackException) { return null; }
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
