using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services;
using AgentSmith.Infrastructure.Core.Services.Skills;
using AgentSmith.Tests.Prompts;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Skills;

/// <summary>
/// 2026-11-05-862ec: the design-partner frame as the PINNED catalog renders it. The master carries role, stance,
/// reading and the outcome list; every format lives in a design-outcome reference that outcome_format serves on
/// the turn that drafts. v5.23.0 resolved to 42,829 characters; the split frame must stay at most 26,000.
/// </summary>
public sealed class DesignPartnerFrameSizeTests : IDisposable
{
    private const int FrameCap = 26_000;

    private static readonly string[] OutcomeReferences =
        ["design-outcome-spec", "design-outcome-epic", "design-outcome-tickets", "design-outcome-bug", "design-outcome-comment"];

    private readonly string _cacheDir = Path.Combine(Path.GetTempPath(), $"agentsmith-frame-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_cacheDir)) Directory.Delete(_cacheDir, recursive: true);
    }

    [Fact]
    public async Task DesignPartnerFrameSize_EmbeddedCatalog_BodyAtMost26000CharsAndCarriesNoFormat()
    {
        var references = await ReferencesAsync();
        var body = new SkillBodyResolver(references).ResolveBody(
            new RoleSkillDefinition { Name = "design-partner-master", Rules = PackagedMaster.Read("design-partner-master") },
            SkillRole.Master);

        body.Length.Should().BeLessThanOrEqualTo(FrameCap, $"the pin is {PackagedMaster.Pin} and the frame is split");
        body.Should().NotContain("```outcome").And.NotContain("kind: epic").And.NotContain("## Drafting a spec");
    }

    [Fact]
    public async Task DesignPartnerFrameSize_EmbeddedCatalog_EveryOutcomeReferenceExists()
    {
        var references = await ReferencesAsync();

        OutcomeReferences.Should().OnlyContain(slug => references.TryRead(slug) != null,
            "outcome_format answers from these references; a missing one falls back to instructions the frame no longer has");
    }

    [Fact]
    public void DesignPartnerFrame_EmbeddedCatalog_DropsNarrowReadingOneReplyConvergenceAndOneSpecAcrossRepositories()
    {
        var master = PackagedMaster.Read("design-partner-master");

        master.Should().NotContain("read\n   narrowly").And.NotContain("## Grounding — cheap tier first");
        master.Should().NotContain("The work has **converged** when the operator has replied");
        master.Should().NotContain("still one spec").And.NotContain("however many\nrepositories");
        master.Should().Contain("**Uploads first.**").And.Contain("## Work picture").And.Contain("`state_work_picture`");
    }

    [Theory]
    [InlineData("document", "The whole document stands inside ONE fence of four")]
    [InlineData("epic", "## Drafting a spec")]
    [InlineData("epic", "## Proposing an epic")]
    [InlineData("tickets", "kind: tickets")]
    public async Task OutcomeFormat_EmbeddedCatalog_ServesTheKindsReference(string kind, string expected)
    {
        var tool = new OutcomeFormatToolHost(await ReferencesAsync()).GetTools(null, null).Single();

        var answer = (await tool.InvokeAsync(new AIFunctionArguments { ["kind"] = kind }))?.ToString();

        answer.Should().Contain(expected).And.NotBe(OutcomeFormatToolHost.InOwnInstructions);
    }

    private async Task<ISkillReferenceSource> ReferencesAsync()
    {
        var handler = new EmbeddedSourceHandler(
            new EmbeddedSkillsCatalog(),
            new CatalogTarballExtractor(NullLogger<CatalogTarballExtractor>.Instance),
            new SkillsCacheMarker(NullLogger<SkillsCacheMarker>.Instance),
            NullLogger<EmbeddedSourceHandler>.Instance);
        var resolution = await handler.ResolveAsync(
            new SkillsConfig { Source = SkillsSourceMode.Embedded, CacheDir = _cacheDir }, CancellationToken.None);
        var catalogPath = new Mock<ISkillsCatalogPath>();
        catalogPath.Setup(p => p.Root).Returns(resolution.Root);
        return new CatalogSkillReferenceSource(catalogPath.Object);
    }
}
