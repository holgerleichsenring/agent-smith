using AgentSmith.Application.Models;
using AgentSmith.Application.Models.Sandbox;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Sandbox.Registry;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-10-12-50bdb: what write_context_yaml says about a context's stack.image. Before the
/// write, an image its registry does not have is a defect — no sandbox can ever pull it, so the
/// model is sent back with the tags the repository does list. After the write, an image that
/// could not be checked is said without asking for a rewrite, and an image outside the
/// registry boundary is said with its lever (2026-10-12-6bfac).
/// </summary>
public sealed class ContextImageReview(ContextImageBoundary boundary, IImageReferenceCheck check)
{
    private const string BoundaryInstruction =
        "Write the context again with an image inside the boundary only if one carries this "
        + "stack's whole toolchain — never one that lacks it. Otherwise keep this image and name "
        + "the boundary and the lever above in your summary, so the operator can act on it.";

    private const string NeverCompose =
        "Never compose a tag from version numbers read in the repository: a tag exists only if the "
        + "registry publishes it.";

    /// <summary>The registry's word on the image; null when the document names none.</summary>
    public async Task<ImageReferenceVerdict?> VerifyAsync(string? image, CancellationToken cancellationToken) =>
        string.IsNullOrWhiteSpace(image) ? null : await check.CheckAsync(image.Trim(), cancellationToken);

    /// <summary>The defect a missing image is, or null when the registry did not say "missing".</summary>
    public string? Defect(ImageReferenceVerdict? verdict)
    {
        if (verdict is not { State: ImageReferenceState.Missing }) return null;
        var head = $"/stack/image: '{verdict.Image}' does not exist — {verdict.Detail}, so no sandbox can ever pull it.";
        if (!verdict.TagsListed)
            return $"{head} Its tag list could not be read; name a tag you know the registry publishes. {NeverCompose}";
        if (verdict.Candidates.Count == 0)
            return $"{head} The repository lists no tag close to it; name a published tag or another image. {NeverCompose}";
        return $"{head} Tags that repository does list, closest first: {string.Join(", ", verdict.Candidates)}. "
               + $"Choose one whose toolchain fits this stack. {NeverCompose}";
    }

    /// <summary>The tool result for a successful write, and the boundary refusal the outcome carries.</summary>
    public ContextImageAnswer Answer(string written, string? image, ImageReferenceVerdict? verdict)
    {
        var refusal = boundary.Judge(image);
        var text = written;
        if (verdict is { State: ImageReferenceState.Unverified })
            text += $"\nNote: stack.image '{verdict.Image}' could not be checked against its registry "
                    + $"({verdict.Detail}); it was written as given, and no rewrite is needed for that.";
        if (refusal is not null)
            text += $"\nNote: {refusal.Describe()}.\n{BoundaryInstruction}";
        return new ContextImageAnswer(text, refusal);
    }
}
