using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Services.Admin;

/// <summary>2026-11-17-f3a8a: the user prompt of an admin turn — the conversation and what this turn answers.</summary>
public static class AdminTurnPrompt
{
    public static string Build(PipelineContext pipeline)
    {
        var transcript = DialogTranscriptSection.Of(pipeline);
        return $"""
            ## Admin conversation
            The transcript of this conversation with an administrator of this installation, oldest first.
            Respond to the LAST user turn; earlier turns are context you already produced or received.

            {DialogTranscriptSection.Render(pipeline, transcript, stewarded: false)}

            ## Your reply
            Write the reply text now — it is delivered verbatim to the conversation.
            """;
    }
}
