using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;

namespace AgentSmith.Server.Services.Admin;

/// <summary>
/// 2026-11-17-f3a8a: which runner takes a conversation's turn — the admin assistant's for the installation's own
/// conversation, the design partner's for every project's.
/// </summary>
public sealed class ConversationTurnRunner(SpecDialogTurnRunner design, AdminTurnRunner admin) : ISpecDialogTurnRunner
{
    public Task<SpecDialogTurnResult?> RunTurnAsync(ConversationState state, TurnPoster poster, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        return InstallationScope.Is(state.Project)
            ? admin.RunTurnAsync(state, poster, cancellationToken)
            : design.RunTurnAsync(state, poster, cancellationToken);
    }
}
