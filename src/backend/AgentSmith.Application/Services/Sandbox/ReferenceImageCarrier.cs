using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Prompts;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-10-08-e8b9k: writes the images an approval cites into the carrying repository's sandbox —
/// <c>.agentsmith/reference/images/&lt;set id&gt;.&lt;ext&gt;</c>, outside the commit like the sets —
/// and makes each an attachment the master's vision path can show.
/// <para>
/// AN IMAGE THIS PROCESS CANNOT READ IS A NOTE, NOT A FAILURE. A set is what the run builds
/// against; an image is shown. A process with no reference store, or an image gone since,
/// leaves a line in the prompt and the run goes on.
/// </para>
/// </summary>
public sealed class ReferenceImageCarrier(
    IReferenceSetReader sets, ISandboxBinaryFileWriter bytes, ReferenceGitExclusion exclusion)
{
    private static readonly Dictionary<string, string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/png"] = ".png", ["image/jpeg"] = ".jpg", ["image/gif"] = ".gif", ["image/webp"] = ".webp",
    };

    public async Task<IReadOnlyList<CarriedUploadImage>> CarryAsync(
        SpecApprovalRecord record, ISandbox sandbox, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (record.CitedImages.Count == 0) return [];
        await exclusion.EnsureAsync(sandbox, ct);
        var session = record.Approval?.Conversation;
        var carried = new List<CarriedUploadImage>(record.CitedImages.Count);
        foreach (var setId in record.CitedImages)
            carried.Add(await CarryOneAsync(session, setId, sandbox, ct));
        return carried;
    }

    private async Task<CarriedUploadImage> CarryOneAsync(string? session, string setId, ISandbox sandbox, CancellationToken ct)
    {
        var image = string.IsNullOrEmpty(session) ? null : await sets.ImageAsync(session, setId, ct);
        if (image is null) return new CarriedUploadImage(new CarriedReferenceImage(setId, null, false), null);
        var name = setId + Extensions.GetValueOrDefault(image.MediaType, ".img");
        if (await bytes.WriteAsync(sandbox, UploadImageAddress.Directory, name, image.Content, ct) is not null)
            return new CarriedUploadImage(new CarriedReferenceImage(setId, null, false), null);
        var attachment = new TicketImageAttachment(
            new AttachmentRef(UploadImageAddress.Prefix + setId, name, image.MediaType, image.Content.LongLength), image.Content);
        return new CarriedUploadImage(new CarriedReferenceImage(setId, $"{UploadImageAddress.Directory}/{name}", false), attachment);
    }
}
