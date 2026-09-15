using AgentSmith.Application.Services;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.TestHelpers;

/// <summary>
/// p0379: canned <see cref="BootstrapPrinciplesTransfer"/> instances for
/// bootstrap-round tests. NoTemplates() models a catalog that ships no core
/// template — the skill then writes principles.md itself;
/// Composing(content) models a catalog that ships the authored core+delta.
/// 2026-08-28-7675: both carry a named catalog origin, because the mode where the
/// skill writes now reports which catalog offered nothing.
/// </summary>
internal static class PrinciplesTransferStubs
{
    public const string CatalogOrigin = "stub-catalog";

    public static BootstrapPrinciplesTransfer NoTemplates() =>
        new(new StubPrinciplesTemplateSource(null), new StubCatalogPath(), Writer(),
            NullLogger<BootstrapPrinciplesTransfer>.Instance);

    public static BootstrapPrinciplesTransfer Composing(string composedContent) =>
        new(new StubPrinciplesTemplateSource(composedContent), new StubCatalogPath(), Writer(),
            NullLogger<BootstrapPrinciplesTransfer>.Instance);

    /// <summary>
    /// 2026-09-15-d66f: a real writer over a stub reader factory. These stubs model catalogs
    /// that declare no artefact, so the writer returns before it touches the sandbox — but it
    /// is the real type, so a change to its contract breaks here rather than silently passing.
    /// </summary>
    public static BootstrapArtefactWriter Writer() =>
        new(new Mock<ISandboxFileReaderFactory>().Object,
            NullLogger<BootstrapArtefactWriter>.Instance);

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
