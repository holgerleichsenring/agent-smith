using System.Text.RegularExpressions;
using AgentSmith.Application.Services.Builders;
using FluentAssertions;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// p0194: every toolchain image in SandboxSpecBuilder.LanguageImages MUST
/// bundle git, because CheckoutSourceHandler runs `git clone` inside the
/// sandbox. A -slim or -alpine variant would break checkout silently — the
/// operator only finds out after 5 minutes of pipeline wait time with
/// "failed to start 'git': No such file or directory".
///
/// Positive allowlist: re-introducing a slim/alpine image for any language
/// fails the build. Adding a new language with an unknown base also fails
/// until the new base is explicitly added to the allowlist.
/// </summary>
public sealed class SandboxSpecBuilderImageBundlesGitTests
{
    private static readonly Regex[] GitBearingPatterns =
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
        foreach (var (language, image) in SandboxSpecBuilder.KnownLanguages)
        {
            if (!GitBearingPatterns.Any(p => p.IsMatch(image)))
                violations.Add($"  - {language} → {image}");
        }

        violations.Should().BeEmpty(
            "every toolchain image must bundle git (CheckoutSourceHandler runs " +
            "`git clone` inside the sandbox). If a new image is added that does " +
            "not match an existing allowlist pattern, either pick a git-bearing " +
            "variant (drop -slim / -alpine, use *-bookworm or *-bullseye) or add " +
            "a new pattern to GitBearingPatterns once you have confirmed the " +
            "image ships with git. Violations:" + Environment.NewLine +
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
        GitBearingPatterns.Any(p => p.IsMatch(image)).Should().BeFalse(
            $"'{image}' is known to ship without git and must not pass the allowlist");
    }
}
