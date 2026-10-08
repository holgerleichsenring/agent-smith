namespace AgentSmith.Server.Models;

/// <summary>2026-10-08-e8b9j: whether the conversation's model sees attached images, and images inside sets.</summary>
public sealed record DialogImageSightView(bool Images, bool UploadedImages)
{
    /// <summary>A project no longer configured: nothing is claimed that cannot be read.</summary>
    public static DialogImageSightView Unknown { get; } = new(true, true);
}
