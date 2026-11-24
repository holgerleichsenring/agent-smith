using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Pocs;

/// <summary>
/// 2026-11-23-933db: the rules a PoC archive's entries are held to, composed in display order — paths, entry kinds,
/// size bounds, forbidden names — on the sizes the central directory declares (upload) or the sizes inflated
/// (intake); and the binary bound, which needs the inflated content and runs at the intake only.
/// </summary>
public sealed class PocArchiveRules(
    PocArchivePaths paths, PocEntryKinds kinds, PocArchiveBounds bounds, PocForbiddenNames forbidden, PocBinaryFiles binary)
{
    internal PocRuleRefusal? RefusalOf(IReadOnlyList<PocArchiveItem> items, long archiveBytes, PocArchiveLimits limits) =>
        paths.RefusalOf(items, limits) ?? kinds.RefusalOf(items) ?? bounds.RefusalOf(items, archiveBytes, limits)
        ?? forbidden.RefusalOf(items);

    internal PocRuleRefusal? BinaryRefusalOf(IReadOnlyList<PocInflatedFile> files, PocArchiveLimits limits) =>
        binary.RefusalOf(files, limits);
}
