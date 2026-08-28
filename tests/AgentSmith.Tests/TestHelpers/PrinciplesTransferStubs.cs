using AgentSmith.Application.Services;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.TestHelpers;

/// <summary>
/// p0379: canned <see cref="BootstrapPrinciplesTransfer"/> instances for
/// bootstrap-round tests. NoTemplates() models a catalog that ships no core
/// template — the skill then writes coding-principles.md itself;
/// Composing(content) models a catalog that ships the authored core+delta.
/// 2026-08-28-7675: both carry a named catalog origin, because the mode where the
/// skill writes now reports which catalog offered nothing.
/// </summary>
internal static class PrinciplesTransferStubs
{
    public const string CatalogOrigin = "stub-catalog";

    public static BootstrapPrinciplesTransfer NoTemplates() =>
        new(new StubPrinciplesTemplateSource(null), new StubCatalogPath(),
            NullLogger<BootstrapPrinciplesTransfer>.Instance);

    public static BootstrapPrinciplesTransfer Composing(string composedContent) =>
        new(new StubPrinciplesTemplateSource(composedContent), new StubCatalogPath(),
            NullLogger<BootstrapPrinciplesTransfer>.Instance);

    internal sealed class StubCatalogPath : ISkillsCatalogPath
    {
        public string Root => "/stub";

        public string Origin => CatalogOrigin;
    }

    private sealed class StubPrinciplesTemplateSource(string? content) : IPrinciplesTemplateSource
    {
        public ComposedPrinciples? Compose(string languageSlug) =>
            content is null ? null : new ComposedPrinciples(content, languageSlug, DeltaApplied: true);
    }
}
