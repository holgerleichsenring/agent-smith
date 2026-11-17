using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Turns;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;

namespace AgentSmith.Server.Services.Admin;

/// <summary>
/// 2026-11-17-f3a8a: what one admin turn hands its pipeline — the transcript, the reply slot, the dialogue job id,
/// the transcript window, the thread its previous turn ended with, and the messages sent while it runs. Nothing of a
/// project: the turn reads the installation through its tools.
/// </summary>
internal static class AdminTurnSeeds
{
    internal static Dictionary<string, object> Build(
        ConversationState state, SpecDialogReplySlot slot, AgentConfig agent, DialogThread? thread,
        TurnPoster poster, IMidTurnMessages messages)
    {
        var seeds = new Dictionary<string, object>
        {
            [ContextKeys.SpecDialogTranscript] = SpecDialogTurnSeeds.MapTranscript(state.Transcript),
            [ContextKeys.SpecDialogReplySlot] = slot,
            [ContextKeys.DialogueJobId] = state.JobId,
            [ContextKeys.SpecDialogTranscriptWindow] = agent.DialogTranscript,
            [ContextKeys.SpecDialogOperatorMessages] = messages,
        };
        if (thread is not null) seeds[ContextKeys.SpecDialogThread] = thread;
        if (poster.MayStartRuns) seeds[ContextKeys.MayStartRuns] = true;
        return seeds;
    }
}
