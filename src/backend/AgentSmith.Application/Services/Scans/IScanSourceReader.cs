namespace AgentSmith.Application.Services.Scans;

/// <summary>
/// 2026-09-01-85b2: the source a scan can read when it checks one of its own findings.
/// <para>
/// One method, because that is the whole question: give me the file this finding cites, or
/// tell me you cannot. The evidence check has no business writing, listing or probing, and
/// an interface offering those would invite it to.
/// </para>
/// </summary>
public interface IScanSourceReader
{
    /// <summary>The content of the cited file, or null when no sandbox in the run has it.</summary>
    Task<string?> TryReadAsync(string citedPath, CancellationToken cancellationToken);
}
