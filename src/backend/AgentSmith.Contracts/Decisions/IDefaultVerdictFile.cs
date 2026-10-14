using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Contracts.Decisions;

/// <summary>
/// 2026-10-13-8762p: the repository copy of a decided default — one entry in
/// <c>.agentsmith/decisions/&lt;fileId&gt;.yaml</c>, written unless the file already holds its marker. Never the
/// decision logger: its event mirror would add a third record of the verdict on the recording run.
/// </summary>
public interface IDefaultVerdictFile
{
    /// <summary>The repository path written, or null when the marker was already there or nothing could be written.</summary>
    Task<string?> AppendAsync(
        ISandboxFileReader files, string fileId, string marker, string entry, CancellationToken cancellationToken);
}
