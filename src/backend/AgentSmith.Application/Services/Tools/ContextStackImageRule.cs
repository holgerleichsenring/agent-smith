using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-08-25-c9c7: a context that describes a stack names the image that stack
/// runs in. Returns the defect, or null when the document satisfies the rule.
/// <para>
/// p0265 stated the rule and the write path checked it, but only once a stack
/// block was present — so a document that omitted the block entirely escaped it,
/// and the sandbox fell back to the language convention table. The block is now
/// mandatory too.
/// </para>
/// <para>
/// p0504's exemption survives unchanged: a context declaring <c>meta.domain</c>
/// gets its toolchain image from that domain's catalog profile, so it may name
/// neither a stack nor an image.
/// </para>
/// </summary>
public sealed class ContextStackImageRule
{
    // 2026-08-25-014d: the tag shapes this used to enumerate are gone. They named four
    // ecosystems' conventions as if they were the rule, and a model that reads them
    // picks an image by how its name looks rather than by what it contains.
    private const string ImageGuidance =
        "name the exact toolchain Docker image whose runtime can BOTH build AND run this "
        + "stack's tests (e.g. mcr.microsoft.com/dotnet/sdk:8.0, node:20-bookworm). It must "
        + "come from a registry the operator trusts and must carry git, because the repository "
        + "is cloned inside it. A context that declares meta.domain is exempt: its profile "
        + "brings an image.";

    public string? Defect(ContextYamlDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!string.IsNullOrWhiteSpace(document.Meta?.Domain)) return null;
        if (document.Stack is null)
            return "/stack: a stack block is required — describe the stack this context "
                 + "builds and " + ImageGuidance;
        return string.IsNullOrWhiteSpace(document.Stack.Image)
            ? "/stack/image: stack.image is required — " + ImageGuidance
            : null;
    }
}
