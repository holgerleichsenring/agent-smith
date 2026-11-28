using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Prompts;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>p0317: what the whole ticket adds to a master's prompt, lifted out of AgenticMasterHandler.</summary>
public sealed class MasterTicketExtras(
    ITicketDocumentMaterializer documentMaterializer, Sandbox.ReferenceGitExclusion exclusion)
{
    /// <summary>2026-11-28-db88b: where a ticket's documents are written — beside the uploaded images, in the
    /// directory the clone's own exclude keeps out of every commit, not in a run record under .agentsmith/runs/.</summary>
    public const string DocumentDirectory = AgentSmith.Sandbox.Wire.ReferenceDirectory.Path + "/documents";

    // p0317: gathers what FetchTicket published — the conversation section, the
    // attachments section (documents materialized into DocumentDirectory first),
    // and the image content parts (vision-capable models only). Scan masters skip
    // document materialization (read-only surface); their conversation section is
    // rendered by ScanMasterPromptFactory instead.
    public async Task<(string Conversation, string Attachments, IReadOnlyList<AIContent> ImageParts)>
        ComposeAsync(
            AgenticMasterContext context, ISandbox sandbox,
            string repoPrefix, bool isScanMaster, CancellationToken cancellationToken)
    {
        var images = MasterPipelineFacts.ListFrom<TicketImageAttachment>(context.Pipeline, ContextKeys.Attachments);
        var documents = MasterPipelineFacts.ListFrom<TicketDocumentAttachment>(context.Pipeline, ContextKeys.TicketDocuments);
        var refs = MasterPipelineFacts.ListFrom<AttachmentRef>(context.Pipeline, ContextKeys.TicketAttachmentRefs);

        IReadOnlyList<MaterializedTicketDocument> materialized = [];
        if (!isScanMaster && documents.Count > 0)
        {
            await exclusion.EnsureAsync(sandbox, cancellationToken);
            materialized = await documentMaterializer.MaterializeAsync(
                sandbox, DocumentDirectory, documents, cancellationToken);
        }
        if (repoPrefix.Length > 0)
            materialized = materialized.Select(m => m with { Path = repoPrefix + m.Path }).ToList();

        var imageParts = context.AgentConfig.SupportsVision
            ? TicketImagePromptParts.Build(images)
            : [];

        return (
            isScanMaster ? string.Empty : TicketConversationPromptSection.Render(context.Pipeline),
            TicketAttachmentPromptSection.Render(
                images, imageParts.Count, materialized, MasterPipelineFacts.OtherBinaries(refs, materialized)),
            imageParts);
    }
}
