using System.Text.RegularExpressions;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Skills;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Skills;

/// <summary>
/// p0379: content assertions on the authored principles templates in the
/// skills catalog (universal core + language deltas) and on the deterministic
/// composition the backend performs with them. Tests resolve the catalog via
/// TestSkillsRoot (AGENTSMITH_TEST_SKILLS_DIR / ./test-skills / adjacent
/// checkout) and SKIP with a console note when the checkout predates the
/// p0379 templates — they must not fail an older pinned catalog.
/// </summary>
public sealed class PrinciplesTemplateContentTests
{
    // The spec's membership test: the moment a rule names a MECHANISM it
    // belongs in a delta, never in the intent-only core.
    private const string MechanismPattern =
        @"\bclass(es)?\b|\bcatch\b|Contracts/|IOptions|MediatR|PascalCase|camelCase|snake_case|" +
        @"\bcsproj\b|test project|one type per file|\brecord\b|\bnamespace\b";

    [Fact]
    public void Core_ContainsNoMechanismWords_ClassCatchContractsPascalCaseTestProject()
    {
        if (!TryReadTemplate("core.md", out var core)) return;

        var leaks = Regex.Matches(core, MechanismPattern, RegexOptions.IgnoreCase)
            .Select(m => m.Value).Distinct().ToList();
        leaks.Should().BeEmpty(
            "the core is intent-only — mechanism words belong in a language delta");
    }

    [Fact]
    public void CorePlusDotnetDelta_ReconstructsCurrentPrinciplesIntent_NothingLost()
    {
        if (!PrinciplesTemplatesAvailable()) return;

        // Ground truth: agent-smith's OWN principles file — the source the
        // core + .NET delta were lifted from. Every anchor must characterize
        // today's file AND survive into the composed core+csharp output.
        var today = File.ReadAllText(OwnPrinciplesPath());
        var composed = NewTemplateSource().Compose("csharp")!.Content.Replace("*", string.Empty);

        var anchors = new[]
        {
            "English", "20 lines per method", "120 lines", "one type per file",
            "single responsibility", "open/closed", "Liskov", "interface segregation",
            "dependency inversion", "tell, don", "composition over inheritance",
            "convention over configuration", "PascalCase", "camelCase", "_camelCase",
            "ITicketProvider", "FetchTicketAsync", "IOptions", "MediatR", "Contracts/",
            "constructor injection", "manual `new`", "Transient", "empty `catch`",
            "narrowest", "OperationCanceledException", "Helper", "Utils", "Manager",
            "Arrange-Act-Assert", "{Method}_{Scenario}_{ExpectedResult}", "{Class}Tests",
            "primary constructors", "file-scoped namespaces", "sealed", "record",
            "guard clauses", "magic values", "Nullable Reference Types", "30 lines",
            "business logic", "Console.WriteLine", "commented-out code",
        };

        foreach (var anchor in anchors)
        {
            today.Should().ContainEquivalentOf(anchor,
                $"anchor '{anchor}' must characterize today's coding-principles.md");
            composed.Should().ContainEquivalentOf(anchor,
                $"anchor '{anchor}' from today's principles must survive in core + .NET delta");
        }
    }

    [Fact]
    public void RustDelta_SuspendsOneTypePerFileAndClassLineLimit_TestsInFile_SnakeCase_NoUnwrap()
    {
        if (!TryReadTemplate(Path.Combine("deltas", "rust.md"), out var rust)) return;

        rust.Should().ContainEquivalentOf("one type per file")
            .And.ContainEquivalentOf("SUSPENDED", "modules replace one-type-per-file");
        rust.Should().Contain("120 lines", "the per-class line cap is explicitly replaced");
        rust.Should().Contain("#[cfg(test)]", "unit tests live in-file");
        rust.Should().Contain("snake_case");
        rust.Should().Contain(".unwrap()", "unwrap is banned in library code");
        rust.Should().Contain("Result", "errors flow as values via Result/?");
    }

    [Fact]
    public void Deltas_FollowDeltaFormat_AdditionsAndOverridesSections()
    {
        if (!PrinciplesTemplatesAvailable()) return;

        foreach (var slug in new[] { "csharp", "rust", "typescript" })
        {
            TryReadTemplate(Path.Combine("deltas", $"{slug}.md"), out var delta).Should()
                .BeTrue($"the {slug} delta must ship with the catalog");
            delta.Should().Contain("## Additions", $"{slug}: DELTA-FORMAT requires Additions");
            delta.Should().Contain("## Overrides", $"{slug}: DELTA-FORMAT requires Overrides");
        }
    }

    [Fact]
    public void Compose_SameStackTwoRepos_IdenticalCoreAndDelta_OnlyRatifiedSpecificsDiffer()
    {
        if (!PrinciplesTemplatesAvailable()) return;

        // Two independent source instances model two repos of the same stack:
        // the composition is deterministic — byte-identical — and the only
        // place repos may diverge is the ratified Project Specifics section.
        var first = NewTemplateSource().Compose("csharp");
        var second = NewTemplateSource().Compose("csharp");

        first.Should().NotBeNull();
        first!.Content.Should().Be(second!.Content);
        first.DeltaApplied.Should().BeTrue();
        first.Content.Should().Contain("status=proposed", "the operator ratifies via the init PR");
        first.Content.Should().Contain("## Project Specifics (ratified additions)");
    }

    [Fact]
    public void Compose_LanguageAliases_ResolveToTheSameDelta()
    {
        if (!PrinciplesTemplatesAvailable()) return;

        var source = NewTemplateSource();
        source.Compose("C#")!.Content.Should().Be(source.Compose("csharp")!.Content);
        source.Compose("dotnet")!.LanguageSlug.Should().Be("csharp");
    }

    [Fact]
    public void Compose_UnknownLanguage_ComposesCoreAloneDeterministically()
    {
        if (!PrinciplesTemplatesAvailable()) return;

        var composed = NewTemplateSource().Compose("cobol");

        composed.Should().NotBeNull("the universal core applies to every language");
        composed!.DeltaApplied.Should().BeFalse();
        composed.Content.Should().Contain("No language delta exists for 'cobol'");
    }

    private static CatalogPrinciplesTemplateSource NewTemplateSource() =>
        new(new CheckoutCatalogPath(),
            NullLogger<CatalogPrinciplesTemplateSource>.Instance);

    /// <summary>ISkillsCatalogPath rooted at the resolved skills checkout
    /// (the directory that CONTAINS the skills/ subtree).</summary>
    private sealed class CheckoutCatalogPath : ISkillsCatalogPath
    {
        public string Root { get; } =
            Path.GetDirectoryName(TestSkillsRoot.Resolve()!.TrimEnd(Path.DirectorySeparatorChar))!;

        public string Origin => Root;
    }

    [Fact]
    public void PrinciplesTemplates_AreFoundInTheCatalog()
    {
        // The guard that tells the two skips apart. No checkout is a legitimate skip;
        // a checkout whose principles this file cannot find is the defect that made
        // every other test here assert nothing, and it looked exactly the same.
        if (TestSkillsRoot.Resolve() is null) return;

        PrinciplesDirectory().Should().NotBeNull(
            "a resolvable skills checkout carries principles, and failing to find them "
            + "silently turns every assertion in this file into an early return");
    }

    private static string? PrinciplesDirectory()
    {
        var skillsRoot = TestSkillsRoot.Resolve();
        if (skillsRoot is null) return null;
        var catalogRoot = Path.GetDirectoryName(skillsRoot.TrimEnd(Path.DirectorySeparatorChar));
        if (catalogRoot is null) return null;
        // 2026-08-28-6403: resolved from the catalog ROOT, the way the production reader
        // resolves it — pointing at the pre-4.0.0 path made every test in this file return
        // early once the catalog moved principles/ to the root.
        // 2026-08-28-489a: one location, because the reader now probes one.
        var dir = Path.Combine(catalogRoot, "principles");
        return File.Exists(Path.Combine(dir, "core.md")) ? dir : null;
    }

    private static bool PrinciplesTemplatesAvailable()
    {
        if (PrinciplesDirectory() is not null) return true;
        Console.WriteLine(
            "PrinciplesTemplateContentTests SKIPPED: skills checkout has no " +
            "principles/core.md (no checkout). " +
            "Point AGENTSMITH_TEST_SKILLS_DIR at an agent-smith-skills checkout with the p0379 templates.");
        return false;
    }

    private static bool TryReadTemplate(string relativePath, out string content)
    {
        content = string.Empty;
        if (!PrinciplesTemplatesAvailable()) return false;
        var path = Path.Combine(PrinciplesDirectory()!, relativePath);
        if (!File.Exists(path)) return false;
        content = File.ReadAllText(path);
        return true;
    }

    private static string OwnPrinciplesPath()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(
            Directory.GetCurrentDirectory().Split("bin")[0], "..", ".."));
        return Path.Combine(repoRoot, ".agentsmith", "contexts", "default", "coding-principles.md");
    }
}
