using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-13-ed5a: what one spec-dialog turn hands the pipeline through
/// <c>PipelineRequest.Context</c> — transcript, reply slot, dialogue job id, and the
/// addressable set. Pure functions over values, lifted out of
/// <see cref="SpecDialogTurnRunner"/>, which sits at its file-length baseline and may only
/// get shorter.
/// <para>
/// The templates go into <see cref="ContextKeys.Sandboxes"/> beside the scope's repos, which
/// is the one preset where that is safe: spec-dialog carries no CommitAndPR, no toolchain
/// probe and no bootstrap handler — the handlers that made 2026-09-13-6f35 keep a template
/// OUT of that map on the code preset. The repos are inserted first, so
/// <c>sandboxes.Keys.First()</c> — the master's default address — is still a repository.
/// </para>
/// </summary>
internal static class SpecDialogTurnSeeds
{
    internal static Dictionary<string, ISandbox> Sandboxes(
        IReadOnlyList<RepoConnection> scopeRepos,
        Func<RepoConnection, ISandbox> repoSandbox,
        IReadOnlyDictionary<string, ISourceScopeSandbox> templates)
    {
        var sandboxes = scopeRepos.ToDictionary(r => r.Name, repoSandbox, StringComparer.Ordinal);
        foreach (var (name, template) in templates) sandboxes[name] = template;
        return sandboxes;
    }

    internal static Dictionary<string, object> Build(
        ConversationState state, IReadOnlyList<RepoConnection> scopeRepos,
        Dictionary<string, ISandbox> sandboxes, SpecDialogReplySlot slot, DialogImageSet images,
        IFiledTicketWithdrawal withdrawal)
    {
        var primary = scopeRepos[0];
        var seeds = new Dictionary<string, object>
        {
            [ContextKeys.SpecDialogTranscript] = MapTranscript(state.Transcript),
            [ContextKeys.SpecDialogReplySlot] = slot,
            // 2026-09-20-3af8: the operator's screenshots. Seeded unconditionally, empty set
            // and all: the master applies the vision flag and the per-turn ceiling, and a turn
            // that seeded nothing is indistinguishable from one whose images were dropped.
            [ContextKeys.SpecDialogImages] = images,
            [ContextKeys.DialogueJobId] = state.JobId,
            // 2026-09-22-9519: the way back out of a filing, seeded beside the identity it acts
            // under — the master builds the tool from the two together.
            [ContextKeys.SpecDialogWithdrawal] = withdrawal,
            [ContextKeys.Sandboxes] = (IReadOnlyDictionary<string, ISandbox>)sandboxes,
            // A template is addressed by its own name, like every other entry: the master
            // reads the address list off this map, so an entry missing here is reachable
            // and unnameable, which is the state 2026-09-13-6f35 had to repair separately.
            [ContextKeys.SandboxRepos] = (IReadOnlyDictionary<string, string>)sandboxes.Keys
                .ToDictionary(name => name, name => name, StringComparer.Ordinal),
            // The master addresses repos by name through the tool host; the
            // singular Repository slot only feeds prompt headers/log lines.
            [ContextKeys.Repository] = new Repository(
                new BranchName(primary.DefaultBranch ?? "main"), primary.Url ?? string.Empty),
        };
        // 2026-09-17-042ed: only an edit turn carries one, and the prompt renders its findings.
        if (state.Revising is { } revising) seeds[ContextKeys.SpecDialogRevisedProposal] = revising;
        return seeds;
    }

    private static IReadOnlyList<SpecDialogTurn> MapTranscript(IReadOnlyList<TranscriptTurn> transcript) =>
        [.. transcript.Select(t => new SpecDialogTurn(
            t.Role == TranscriptRole.Assistant ? SpecDialogTurn.AssistantRole : SpecDialogTurn.UserRole,
            t.Text, t.Kind))];
}
