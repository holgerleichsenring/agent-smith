using AgentSmith.Contracts.Models;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Application.Services.Prompts;

/// <summary>
/// 2026-10-08-e8b9k: how an uploaded image the approval cites is told apart from the ticket's own
/// among a run's attachments — its address is <c>upload:&lt;set id&gt;</c> — and where it lies.
/// </summary>
public static class UploadImageAddress
{
    public const string Prefix = "upload:";

    /// <summary>The directory, relative to the carrying repository, the cited images are written to.</summary>
    public const string Directory = ReferenceDirectory.Path + "/images";

    public static bool Is(TicketImageAttachment image)
    {
        ArgumentNullException.ThrowIfNull(image);
        return image.Ref.Uri.StartsWith(Prefix, StringComparison.Ordinal);
    }
}
