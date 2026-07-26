using AgentSmith.Application.Services;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.TestHelpers;

/// <summary>
/// p0379: canned <see cref="BootstrapPrinciplesTransfer"/> instances for
/// bootstrap-round tests. NoTemplates() models a pre-p0379 catalog (no core
/// template → the skill writes coding-principles.md, the legacy shape);
/// Composing(content) models a catalog that ships the authored core+delta.
/// </summary>
internal static class PrinciplesTransferStubs
{
    public static BootstrapPrinciplesTransfer NoTemplates() =>
        new(new StubPrinciplesTemplateSource(null), NullLogger<BootstrapPrinciplesTransfer>.Instance);

    public static BootstrapPrinciplesTransfer Composing(string composedContent) =>
        new(new StubPrinciplesTemplateSource(composedContent), NullLogger<BootstrapPrinciplesTransfer>.Instance);

    private sealed class StubPrinciplesTemplateSource(string? content) : IPrinciplesTemplateSource
    {
        public ComposedPrinciples? Compose(string languageSlug) =>
            content is null ? null : new ComposedPrinciples(content, languageSlug, DeltaApplied: true);
    }
}
