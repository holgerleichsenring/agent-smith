using System.ComponentModel;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-10-08-e8b9j: view_reference_image — an image file inside an upload, shown to the model
/// as a picture. Read from the STORE by conversation and set, not from the set's sandbox, so it
/// works on the in-process backend and opens no container; handed over through the tool-image
/// deposit, which bounds it and tells a model that cannot see it that it exists. The model's
/// vision is the OCR: no OCR program is installed anywhere.
/// </summary>
internal sealed class ReferenceImageToolHost(
    IReadOnlyDictionary<string, ReferenceUploadAddress> uploads, IReferenceSetReader sets, IToolImageDeposit deposit)
    : IToolHost
{
    private const string WorkPrefix = "/work/";

    private static readonly Dictionary<string, string> MediaTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png", [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif", [".webp"] = "image/webp",
    };

    public IEnumerable<AIFunction> GetTools(SkillExecutionPhase? phase, string? investigatorMode) =>
        [AIFunctionFactory.Create(ViewReferenceImage, name: "view_reference_image")];

    [Description("Shows you an image file inside an upload as a picture — a screenshot, a mock, a scanned page — so "
        + "you can look at it and read its text: your vision is the OCR, and no OCR program is installed. The file "
        + "must be PNG, JPEG, GIF or WebP, at most 5 MB and 1568 px on its long edge; a larger screenshot is shown "
        + "whole when the operator attaches it through the Image entry.")]
    public async Task<string> ViewReferenceImage(
        [Description("The upload's address, reference:<name>, as listed under 'Material the operator uploaded'.")] string reference,
        [Description("The file's path in the upload, as <name>/… or /work/<name>/….")] string path,
        CancellationToken ct = default)
    {
        if (!uploads.TryGetValue(reference ?? string.Empty, out var upload))
            return $"Error: '{reference}' is not an upload here. Uploads: [{string.Join(", ", uploads.Keys)}].";
        var relative = (path ?? string.Empty).StartsWith(WorkPrefix, StringComparison.Ordinal)
            ? path![WorkPrefix.Length..] : (path ?? string.Empty).TrimStart('/');
        if (!MediaTypes.TryGetValue(Path.GetExtension(relative), out var mediaType))
            return $"not an image: '{path}' is not a .png, .jpg, .jpeg, .gif or .webp file.";
        var file = await sets.FileAsync(upload.Session, upload.SetId, relative, ct);
        if (file is null) return $"Error: '{path}' is not a file of {reference}.";
        var result = deposit.Deposit(new ToolImage(mediaType, file.Content, $"{relative} from the upload {reference}"));
        return result.IsAccepted
            ? $"image: {relative} follows this result"
            : $"image: not shown — {result.Refusal}. Ask the operator to attach it through the Image entry to have it shown whole.";
    }
}
