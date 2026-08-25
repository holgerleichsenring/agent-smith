using System.Text.RegularExpressions;
using AgentSmith.Application.Services.Sandbox;
using FluentAssertions;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// p0194: every toolchain image in ToolchainImageCatalog.KnownLanguages MUST
/// bundle git, because CheckoutSourceHandler runs `git clone` inside the
/// sandbox. A -slim or -alpine variant would break checkout silently — the
/// operator only finds out after 5 minutes of pipeline wait time with
/// "failed to start 'git': No such file or directory".
///
/// Positive allowlist: re-introducing a slim/alpine image for any language
/// fails the build. Adding a new language with an unknown base also fails
/// until the new base is explicitly added to the allowlist.
///
/// 2026-08-25-014d: the allowlist lives HERE now, not in the product. In the
/// product it was a runtime guess applied to images somebody else named — and
/// its only effect was to swap such an image for a different one, silently. As
/// a test it is what it always really was: a pin on OUR OWN curated table, whose
/// four bases this repository has confirmed ship git.
/// </summary>
public sealed class SandboxSpecBuilderImageBundlesGitTests
{
    private static readonly Regex[] ConfirmedGitBearingBases =
    [
        // Microsoft .NET SDK images include git in every tag.
        new(@"^mcr\.microsoft\.com/dotnet/sdk:", RegexOptions.Compiled),
        // Debian bookworm full base bundles git.
        new(@":[^-]*-bookworm$", RegexOptions.Compiled),
        // Debian bullseye full base bundles git.
        new(@":[^-]*-bullseye$", RegexOptions.Compiled),
        // The -scm suffix on buildpack-deps is explicitly source-control-tooling.
        new(@"^buildpack-deps:[^-]+-scm$", RegexOptions.Compiled),
    ];

    [Fact]
    public void AllLanguageImages_MatchGitBearingAllowlist()
    {
        var violations = new List<string>();
        foreach (var (language, image) in ToolchainImageCatalog.KnownLanguages)
        {
            if (!ConfirmedGitBearingBases.Any(p => p.IsMatch(image)))
                violations.Add($"  - {language} → {image}");
        }

        violations.Should().BeEmpty(
            "every toolchain image in OUR table must bundle git (a sandbox runs " +
            "`git clone` inside itself). If a new image is added that does not " +
            "match an existing allowlist pattern, either pick a git-bearing " +
            "variant (drop -slim / -alpine, use *-bookworm or *-bullseye) or add " +
            "a new pattern here once you have confirmed the image ships with git. " +
            "Violations:" + Environment.NewLine +
            string.Join(Environment.NewLine, violations));
    }

    [Theory]
    [InlineData("node:20-bookworm-slim")]
    [InlineData("python:3.12-slim")]
    [InlineData("node:20-alpine")]
    [InlineData("python:3.12-alpine")]
    [InlineData("node:20")]
    public void AllowlistRejects_KnownNonGitImages(string image)
    {
        // Pins the test itself — if the allowlist starts accepting slim /
        // alpine / bare tags by mistake, this fails immediately.
        ConfirmedGitBearingBases.Any(p => p.IsMatch(image)).Should().BeFalse(
            $"'{image}' is known to ship without git and must not pass the allowlist");
    }

    [Fact]
    public void TheGitGuess_IsNotInTheProduct()
    {
        // 2026-08-25-014d: the catalog offers a language its convention image and
        // nothing else. A name-shaped answer to "does this image contain git" is
        // exactly the guess this phase removed, so its return would be a regression.
        typeof(ToolchainImageCatalog).GetMembers()
            .Select(m => m.Name)
            .Should().NotContain(n => n.Contains("Git", StringComparison.Ordinal),
                "what an image contains is discovered where it is used, not judged by its tag");
    }
}
