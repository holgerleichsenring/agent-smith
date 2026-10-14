using AgentSmith.Contracts.Commands;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Builds the ticket steward's user prompt for one turn of a conversation on a ticket that has run: the transcript,
/// the ticket and what the framework does with it, and a reply contract that answers and acts but never proposes.
/// </summary>
public interface ITicketStewardPromptFactory
{
    /// <summary>The images the conversation holds and how many ride this message, as for the design partner.</summary>
    string Build(PipelineContext pipeline, int imagesExisting, int imagesCarried);
}
