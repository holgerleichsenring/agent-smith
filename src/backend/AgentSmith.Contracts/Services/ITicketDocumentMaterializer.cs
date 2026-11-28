using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// p0317: writes downloaded text-like ticket documents into the attachments/ dir
/// under the given directory of the sandbox so the master can read_file them —
/// txt/md as-is, pdf/docx converted to markdown via markitdown (which runs
/// INSIDE the sandbox; the backend host has no converter). Fail-soft per
/// document: a document that cannot be materialized is logged and skipped,
/// never sinks the run.
/// </summary>
public interface ITicketDocumentMaterializer
{
    Task<IReadOnlyList<MaterializedTicketDocument>> MaterializeAsync(
        ISandbox sandbox,
        string directory,
        IReadOnlyList<TicketDocumentAttachment> documents,
        CancellationToken cancellationToken);
}
