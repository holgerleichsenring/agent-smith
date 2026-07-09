namespace AgentSmith.Contracts.Models;

/// <summary>
/// p0317: a ticket document that was written into the run-record attachments/
/// dir (pdf/docx converted to markdown via markitdown, txt/md as-is), readable
/// by the master via read_file. <paramref name="Path"/> is repo-relative;
/// <paramref name="OriginFileName"/> names the ticket attachment it came from.
/// </summary>
public sealed record MaterializedTicketDocument(
    string Path,
    string OriginFileName);
