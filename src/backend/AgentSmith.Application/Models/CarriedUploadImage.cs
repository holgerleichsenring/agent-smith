using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Models;

/// <summary>2026-10-08-e8b9k: one cited image as the carry wrote it, and the attachment that can show it
/// (null when this process could not read it).</summary>
public sealed record CarriedUploadImage(CarriedReferenceImage Image, TicketImageAttachment? Attachment);
