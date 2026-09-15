namespace AgentSmith.Contracts.Commands;

/// <summary>
/// p0317: what the tracker returned ABOUT the ticket beyond its text — the comment thread and
/// the attachments, in the three shapes the prompt assembly treats differently.
/// <para>
/// 2026-09-15-d66f: carved into its own partial, the way this type's own doc comment says the
/// core file is split, because that file sits at its length baseline and may only get shorter.
/// </para>
/// </summary>
public static partial class ContextKeys
{
    /// <summary>p0317: IReadOnlyList&lt;TicketComment&gt; — the ticket's comment thread,
    /// fetched fail-soft by FetchTicketHandler and rendered (delimited, chronological,
    /// author-attributed) into the master prompts. Absent when the ticket has none.</summary>
    public const string TicketComments = "TicketComments";

    /// <summary>p0317: IReadOnlyList&lt;TicketDocumentAttachment&gt; — downloaded text-like
    /// ticket documents (txt/md/pdf/docx). Materialized into the run-record attachments/
    /// dir (markitdown for pdf/docx) at AgenticMaster time, when a sandbox exists.</summary>
    public const string TicketDocuments = "TicketDocuments";

    /// <summary>p0317: IReadOnlyList&lt;AttachmentRef&gt; — ALL attachment references on
    /// the ticket (images, documents, other binaries). Lets the prompt list
    /// non-viewable binaries by name + size without ever inlining them.</summary>
    public const string TicketAttachmentRefs = "TicketAttachmentRefs";
}
