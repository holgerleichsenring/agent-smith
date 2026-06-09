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
///
/// p0265: the allowlist is now the SHARED <see cref="SandboxSpecBuilder.GitBearingImagePatterns"/>
/// — the same patterns that gate an LLM-named context.yaml stack.image. One
/// source of truth for "this image ships git".
/// </summary>
public sealed class SandboxSpecBuilderImageBundlesGitTests
{
    [Fact]
    public void AllLanguageImages_MatchGitBearingAllowlist()
    {
        var violations = new List<string>();
        foreach (var (language, image) in SandboxSpecBuilder.KnownLanguages)
        {
            if (!SandboxSpecBuilder.GitBearingImagePatterns.Any(p => p.IsMatch(image)))
                violations.Add($"  - {language} → {image}");
        }

        violations.Should().BeEmpty(
            "every toolchain image must bundle git (CheckoutSourceHandler runs " +
            "`git clone` inside the sandbox). If a new image is added that does " +
            "not match an existing allowlist pattern, either pick a git-bearing " +
            "variant (drop -slim / -alpine, use *-bookworm or *-bullseye) or add " +
            "a new pattern to GitBearingImagePatterns once you have confirmed the " +
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
        SandboxSpecBuilder.GitBearingImagePatterns.Any(p => p.IsMatch(image)).Should().BeFalse(
            $"'{image}' is known to ship without git and must not pass the allowlist");
    }

    // p0265: the supply-chain gate for an LLM-named stack.image.
    [Theory]
    [InlineData("mcr.microsoft.com/dotnet/sdk:8.0", true)]
    [InlineData("ghcr.io/some-org/tool:1-bookworm", true)]
    [InlineData("node:20-bookworm", true)]
    [InlineData("buildpack-deps:bookworm-scm", true)]
    [InlineData("evil.example.com/pwn:latest", false)]
    [InlineData("someuser/node:20-bookworm", false)]
    public void IsTrustedRegistry_AcceptsOnlyOfficialSources(string image, bool trusted) =>
        SandboxSpecBuilder.IsTrustedRegistry(image).Should().Be(trusted);
}
