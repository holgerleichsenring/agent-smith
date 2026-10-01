using AgentSmith.Server.Models;
using AgentSmith.Server.Services.References;
using FluentAssertions;
using static AgentSmith.Tests.References.ReferenceUploadRequests;

namespace AgentSmith.Tests.References;

/// <summary>
/// 2026-10-01-283db: what an uploaded set must be. The ignore list drops what an operating system
/// left first; then every file passes or none is stored, and a refusal names the file and the limit.
/// </summary>
public sealed class ReferenceSetValidatorTests
{
    private readonly ReferenceSetValidator _validator =
        new(new ReferencePathRule(), new ReferenceIgnoreList(), new ReferenceFileTypes());

    [Fact]
    public void ReferenceSetValidator_Check_NormalisesBackslashesAndKeepsTheOrder()
    {
        var check = _validator.Check([Part("site\\index.html"), Part("./site/a.css")]);

        check.Files.Select(f => f.Path).Should().Equal("site/index.html", "site/a.css");
    }

    [Theory]
    [InlineData("/etc/passwd", "absolute")]
    [InlineData("C:/site/a.css", "drive")]
    [InlineData("site//a.css", "empty path segment")]
    [InlineData("site/a\0.css", "NUL")]
    public void ReferencePathRule_AnEscapingPath_IsRefusedNamingWhy(string path, string why)
    {
        _validator.Check([Part(path)]).Refusal.Should().Contain(why);
    }

    [Fact]
    public void ReferencePathRule_ADuplicateInAnyCase_IsRefused()
    {
        _validator.Check([Part("site/A.css"), Part("site/a.css")]).Refusal.Should().Contain("appears twice");
    }

    [Fact]
    public void ReferencePathRule_APathOverTheColumn_IsRefusedNotCut()
    {
        _validator.Check([Part("s/" + new string('a', 240) + ".css")]).Refusal.Should().Contain("240-character");
    }

    [Fact]
    public void ReferenceSetValidator_AFileOverFiveMegabytes_IsRefusedNamingTheLimit()
    {
        var big = new ReferenceUploadPart("site/hero.png", new byte[ReferenceUploadLimits.MaxFileBytes + 1]);

        _validator.Check([big]).Refusal.Should().Contain("'site/hero.png'").And.Contain("5 MB per-file limit");
    }

    [Fact]
    public void ReferenceSetValidator_ASetOverTwentyFiveMegabytes_IsRefusedAtTheFileThatCrossesIt()
    {
        var parts = Enumerable.Range(0, 6)
            .Select(i => new ReferenceUploadPart($"site/{i}.png", new byte[ReferenceUploadLimits.MaxFileBytes]))
            .ToList();

        _validator.Check(parts).Refusal.Should().Contain("'site/5.png'").And.Contain("25 MB set limit");
    }

    [Fact]
    public void ReferenceSetValidator_MoreThanFiveHundredFiles_IsRefused()
    {
        var parts = Enumerable.Range(0, 501).Select(i => Part($"site/{i}.css")).ToList();

        _validator.Check(parts).Refusal.Should().Contain("500-file limit");
    }

    [Fact]
    public void ReferenceIgnoreList_IsIgnored_KnowsTheOperatingSystemsLeftovers()
    {
        var ignore = new ReferenceIgnoreList();

        ignore.IsIgnored("site/.DS_Store").Should().BeTrue();
        ignore.IsIgnored("__MACOSX/site/a.css").Should().BeTrue();
        ignore.IsIgnored("site/._a.css").Should().BeTrue();
        ignore.IsIgnored("site/THUMBS.DB").Should().BeTrue();
        ignore.IsIgnored("site/desktop.ini").Should().BeTrue();
        ignore.IsIgnored("site/a.css").Should().BeFalse();
    }

    [Fact]
    public void ReferenceFileTypes_MediaTypeOf_ComesFromTheExtension()
    {
        var types = new ReferenceFileTypes();

        types.MediaTypeOf("site/FAVICON.ICO").Should().Be("image/x-icon");
        types.MediaTypeOf("site/app.mjs").Should().Be("text/javascript");
        types.MediaTypeOf("site/run.php").Should().BeNull();
        ReferenceFileTypes.Allowed.Should().Contain("woff2");
    }

    [Fact]
    public void ReferenceUploadLimits_Megabytes_StatesASizeAsARefusalReadsIt()
    {
        ReferenceUploadLimits.Megabytes(5L * 1024 * 1024).Should().Be("5 MB");
    }

    private static ReferenceUploadPart Part(string path) => new(path, Text("x"));
}
