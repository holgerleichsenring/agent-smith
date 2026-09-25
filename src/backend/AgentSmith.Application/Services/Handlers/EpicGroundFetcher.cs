using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-09-13-7d9f: puts the epic a run's ticket is one slice of onto the pipeline, so the
/// derivation prompt can ground every child of one cut on the same parent.
/// <para>
/// 2026-09-25-d0b8: THE READ ITSELF IS NO LONGER HERE. This class used to own the stamp lookup,
/// the fetch and the degradation note while writing its result into a pipeline context and
/// returning a run step's sentence — so the only way to ask "what epic is this ticket stamped
/// into?" was to be a pipeline. <see cref="EpicParentReader"/> answers that question for any
/// caller; what is left here is the one thing that IS pipeline-shaped: publishing the answer
/// under <see cref="ContextKeys.EpicGround"/> and phrasing it as a step message.
/// </para>
/// </summary>
public sealed class EpicGroundFetcher(EpicParentReader reader)
{
    /// <summary>
    /// Puts the parent's body on the pipeline as <see cref="ContextKeys.EpicGround"/> and
    /// returns what the fetch step should SAY about it — the empty string for the ordinary
    /// ticket that is nobody's slice, so a run outside an epic reads exactly as it did.
    /// </summary>
    public async Task<string> AttachAsync(
        ITicketProvider provider, Ticket ticket, PipelineContext pipeline,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pipeline);

        var read = await reader.ReadAsync(provider, ticket, cancellationToken);
        // 2026-09-18-d518: the parent is a SECOND ticket, published here rather than at the
        // fetch handler's door.
        if (read.Ground is not null) pipeline.Set(ContextKeys.EpicGround, read.Ground);

        // The separator is the STEP MESSAGE's punctuation, not the read's: the fetch step
        // appends this to its own sentence, and a caller that renders the note elsewhere
        // must not inherit an em dash from it.
        return read.Note.Length == 0 ? string.Empty : $" — {read.Note}";
    }
}
