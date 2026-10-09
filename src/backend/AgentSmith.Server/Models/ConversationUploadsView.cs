namespace AgentSmith.Server.Models;

/// <summary>2026-10-08-e8b9g: a conversation's images and sets, and the bytes they hold together.
/// 2026-10-08-e8b9j: and how many images it holds — Image-entry images plus image files inside sets —
/// with whether its model sees them.</summary>
public sealed record ConversationUploadsView(
    IReadOnlyList<SpecDialogImageView> Images, IReadOnlyList<ReferenceSetView> References, long Bytes,
    int ImageCount = 0, DialogImageSightView? Sight = null);
