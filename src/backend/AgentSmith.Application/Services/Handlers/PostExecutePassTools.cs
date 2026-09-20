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
/// The decision log gets the CHANGED repo's file surface, so a decision taken during one of these
/// passes is written into the sandbox that holds the code the pass is changing.
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
        // The sandbox comes off the partition, never out of ChangedSandboxes[repoName]: that
        // dictionary is keyed by SANDBOX KEY and the names are REPO names, and the two agree in
        // only one of SandboxKeyComposer's four shapes.
        var sandbox = partition.FirstChangedSandbox
            ?? throw new ArgumentException("No repo changed; there is no pass to build tools for.",
                nameof(partition));
        return (
            new FilesystemToolHost(partition.ChangedSandboxes, partition.ChangedRepoNames[0], repoLocalPath),
            new LogDecisionToolHost(decisionLogger, readerFactory.Create(sandbox)),
            new HumanToolHost(dialogueTransport));
    }
}
