using AgentSmith.Application.Services.Repositories;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-09-19-c511a: the tool trio the two post-execute passes — docs and tests — build over the
/// repos a run actually changed. It was the same six lines in both handlers, and both were about to
/// gain the same third collaborator; the ratchet's answer to a file that must grow is to extract the
/// responsibility, and this is the one they share.
/// <para>
/// 2026-10-12-f5f7c: the decision log gets every CHANGED repo's file surface by name, so a decision
/// taken during one of these passes is written into the repository it names — not the first one
/// that changed.
/// </para>
/// </summary>
public sealed class PostExecutePassTools(
    IDecisionLogger decisionLogger,
    ISandboxFileReaderFactory readerFactory,
    IDialogueTransport? dialogueTransport)
{
    public (FilesystemToolHost Fs, LogDecisionToolHost Log, HumanToolHost Human) For(
        RepoDiffPartition partition, string repoLocalPath)
    {
        ArgumentNullException.ThrowIfNull(partition);
        if (partition.ChangedRepoNames.Count == 0)
            throw new ArgumentException("No repo changed; there is no pass to build tools for.",
                nameof(partition));
        var changed = partition.ChangedRepoSandboxes.ToDictionary(
            kv => kv.Key, kv => readerFactory.Create(kv.Value), StringComparer.Ordinal);
        return (
            new FilesystemToolHost(partition.ChangedSandboxes, partition.ChangedRepoNames[0], repoLocalPath),
            new LogDecisionToolHost(decisionLogger, new FixedRunRepositories(changed)),
            new HumanToolHost(dialogueTransport));
    }
}
