namespace AgentSmith.Server.Models;

/// <summary>2026-10-08-e8b9g: a conversation's images and sets, and the bytes they hold together.</summary>
public sealed record ConversationUploadsView(
    IReadOnlyList<SpecDialogImageView> Images, IReadOnlyList<ReferenceSetView> References, long Bytes);
