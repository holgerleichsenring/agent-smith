namespace AgentSmith.Contracts.Models;

/// <summary>
/// 2026-09-20-3af8: one image an operator attached to a design conversation, ready for the
/// design partner's vision input.
/// <para>
/// It is NOT a <see cref="TicketImageAttachment"/>, which is built around the
/// <c>AttachmentRef</c> a tracker hands back — a file name, a declared mime type, a size and a
/// download URL. A conversation has no tracker behind it, so every one of those fields would
/// have to be invented to reuse that shape. What IS reused is what the ticket path decided
/// rather than described: the supported kinds and the five-megabyte cap, both of which stay
/// on <see cref="TicketImageAttachment"/> as the one place this estate states them.
/// </para>
/// </summary>
/// <param name="MediaType">The kind the BYTES say it is — never what the uploader called it.</param>
public sealed record DialogImage(string MediaType, byte[] Content);
