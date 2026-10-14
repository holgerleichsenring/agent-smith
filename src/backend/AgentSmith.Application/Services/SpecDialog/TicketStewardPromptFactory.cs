using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// The ticket steward's prompt contract. The conversation belongs to a ticket that has run, so the turn answers the
/// person about its attempts and series and acts with its tools when asked — the series changes through
/// edit_series and an attempt starts through start_next_attempt. It never proposes: a ```yaml or ```outcome block
/// in its reply is quoted text, and nothing reads it as a draft.
/// </summary>
public sealed class TicketStewardPromptFactory : ITicketStewardPromptFactory
{
    private const string Contract =
        "This ticket has already run. Answer the person: what its attempts did, why one ended, what its series "
        + "holds and what the next attempt will run. When the person asks you to change the series or to start "
        + "the next attempt, do it with your tools and report what the tool answered — never report an act no "
        + "tool confirmed, and never claim a tool you were not given. Propose nothing: the series changes only "
        + "through your tools, so a ```yaml or ```outcome block you write is quoted text, not a proposal, and "
        + "nobody is asked to approve it.";

    public string Build(PipelineContext pipeline, int imagesExisting, int imagesCarried)
    {
        var transcript = DialogTranscriptSection.Of(pipeline);
        return $"""
            ## Ticket conversation
            The transcript of this ticket's conversation so far, oldest first. Respond to the
            LAST user turn; earlier turns are context you already produced or received.

            {DialogTranscriptSection.Render(transcript)}
            {SeededTicketSection.Render(pipeline)}
            {FrameworkFactsSection.Render(pipeline)}
            {DialogScopeRepositoriesSection.Render(pipeline)}

            ## Your reply
            Write the reply text now — it is delivered verbatim to the conversation.
            Read source files, the ticket and its runs through your tools when the question needs them.
            {DialogImageNote.Render(imagesExisting, imagesCarried)}
            {Contract}
            """;
    }
}
