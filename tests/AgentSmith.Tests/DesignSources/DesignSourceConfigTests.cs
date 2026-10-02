using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.DesignSources;

/// <summary>
/// 2026-10-01-7f7aa: a design source is a catalog entry a project references by name. The
/// resolved project carries the secret's NAME; an unknown source, or a source naming no
/// secret, is a blocking finding rather than a failure at the first read.
/// </summary>
public sealed class DesignSourceConfigTests
{
    private const string Catalogs = """
        agents:
          a: { type: claude, model: sonnet }
        repos:
          r: { type: github, url: https://github.com/x/y, auth: t }
        trackers:
          t: { type: github, auth: t }
        secrets:
          t: x
          figma_token: figd-literal-token-value
        """;

    [Fact]
    public void Project_ReferencingAFigmaSource_ResolvesWithTheSecretNameOnly()
    {
        var (config, findings) = Resolve("""
            design_sources:
              brand: { vendor: figma, auth: figma_token, display_name: Brand library }
            projects:
              demo: { agent: a, tracker: t, repos: [r], pipeline: code, design_sources: [Brand] }
            """);

        findings.Should().BeEmpty();
        config.Projects["demo"].DesignSources.Should().ContainSingle().Which.Should().Be(
            new DesignSource("brand", DesignSourceVendor.Figma, "figma_token", "Brand library"));
    }

    [Fact]
    public void Project_NamesUnknownDesignSource_IsABlockingFinding()
    {
        var (config, findings) = Resolve("""
            projects:
              demo: { agent: a, tracker: t, repos: [r], pipeline: code, design_sources: [ghost] }
            """);

        config.Projects.Should().NotContainKey("demo");
        findings.Should().ContainSingle(f => f.Severity == StartupFindingSeverity.Blocking
            && f.Project == "demo" && f.Field == "design_sources" && f.Reason.Contains("'ghost'"));
    }

    [Fact]
    public void DesignSource_NamingNoSecretOfTheCatalog_IsABlockingFindingAndNotBuilt()
    {
        var (config, findings) = Resolve("""
            design_sources:
              brand: { vendor: figma, auth: missing_token }
            projects:
              demo: { agent: a, tracker: t, repos: [r], pipeline: code }
            """);

        config.Projects.Should().ContainKey("demo", "a project that names no source is unaffected");
        findings.Should().ContainSingle(f => f.Field == "design_sources:brand"
            && f.Severity == StartupFindingSeverity.Blocking && f.Reason.Contains("'missing_token'"));
    }

    [Fact]
    public void DesignSourceCatalogBuilder_KeysByCaseInsensitiveName()
    {
        var built = new DesignSourceCatalogBuilder().Build(
            new Dictionary<string, RawDesignSourceEntry> { ["Brand"] = new() { Auth = "tok" } },
            ["TOK"], []);

        built.Should().ContainKey("brand");
    }

    private static (AgentSmithConfig, IReadOnlyList<StartupFinding>) Resolve(string yaml)
    {
        var resolver = new ConfigCatalogResolver();
        var config = resolver.Resolve(new RawConfigYaml().Deserialize(Catalogs + "\n" + yaml));
        return (config, resolver.LastFindings);
    }
}
