using AgentSmith.Server.Models;
using AgentSmith.Server.Services.References;
using FluentAssertions;
using static AgentSmith.Tests.References.ReferenceUploadRequests;

namespace AgentSmith.Tests.References;

/// <summary>
/// 2026-10-01-283db: what an uploaded set must be. The ignore list drops what an operating system
/// left first; then every file passes or none is stored, and a refusal names the file and the limit.
/// 2026-10-02-075da: a file is kept unless a rebuildable folder or the per-file bound leaves it out.
/// </summary>
public sealed class ReferenceSetValidatorTests
{
    private readonly ReferenceSetValidator _validator =
        new(new ReferencePathRule(), new ReferenceIgnoreList());

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
    public void ReferenceUpload_SourceFolder_KeepsEveryAuthoredFile()
    {
        var check = _validator.Check([Part("app/form/app.py"), Part("app/.env"), Part("app/Dockerfile"), Part("app/LICENSE")]);

        check.Files.Select(f => f.Path).Should().Equal("app/form/app.py", "app/.env", "app/Dockerfile", "app/LICENSE");
        check.LeftOut.Should().BeEmpty();
    }

    [Fact]
    public void ReferenceUpload_VenvAndGitFolders_AreLeftOutCollapsedWithReason()
    {
        var check = _validator.Check([Part("pong/pong.py"), Part("pong/.venv/lib/a.py"), Part("pong/.venv/lib/b.py"),
            Part("pong/.git/HEAD")]);

        check.Files.Select(f => f.Path).Should().Equal("pong/pong.py");
        check.LeftOut.Should().Equal(
            new ReferenceLeftOut("pong/.git/", ReferenceLeftOut.Rebuildable),
            new ReferenceLeftOut("pong/.venv/", ReferenceLeftOut.Rebuildable));
    }

    [Fact]
    public void ReferenceUpload_OneFileOverTheFileBound_IsLeftOutAndTheRestIsStored()
    {
        var big = new ReferenceUploadPart("m/deck.pdf", new byte[ReferenceUploadLimits.MaxFileBytes + 1]);

        var check = _validator.Check([big, Part("m/page.html")]);

        check.Files.Select(f => f.Path).Should().Equal("m/page.html");
        check.LeftOut.Should().ContainSingle().Which.Reason.Should().Contain("25 MB per-file limit");
    }

    [Fact]
    public void ReferenceUpload_OverTheSetBound_RefusesNamingTheLargestTopLevelEntries()
    {
        var parts = Enumerable.Range(0, 3)
            .Select(i => new ReferenceUploadPart($"app/cache/{i}.bin", new byte[10L * 1024 * 1024]))
            .Append(Part("app/server.js")).ToList();

        _validator.Check(parts).Refusal.Should().Contain("25 MB set limit").And.Contain("'app/cache/' (30 MB)");
    }

    [Fact]
    public void ReferenceUpload_OverLongPathUnderVenv_DoesNotRefuseTheSet()
    {
        var check = _validator.Check([Part("p/.venv/" + new string('a', 260) + ".py"), Part("p/main.py")]);

        check.IsRefused.Should().BeFalse();
        check.Files.Select(f => f.Path).Should().Equal("p/main.py");
    }

    [Fact]
    public void ReferenceUpload_OnlyRebuildableFiles_IsRefusedNamingThem()
    {
        _validator.Check([Part("p/node_modules/x/index.js")]).Refusal.Should()
            .Contain("no file to keep").And.Contain("'p/node_modules/' (rebuildable)");
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
    public void ReferenceFileTypes_MediaTypeOf_ComesFromTheExtensionAndDefaultsToOctetStream()
    {
        var types = new ReferenceFileTypes();

        types.MediaTypeOf("site/FAVICON.ICO").Should().Be("image/x-icon");
        types.MediaTypeOf("site/app.mjs").Should().Be("text/javascript");
        types.MediaTypeOf("site/run.php").Should().Be(ReferenceFileTypes.Untyped);
    }

    [Fact]
    public void ReferenceCredentialFiles_Holds_NamesTheCommonOnes()
    {
        new[] { "a/.env", "a/.env.local", "a/runtime.env", "a/.npmrc", "a/tls.pem", "a/id_rsa", "a/credentials.json" }
            .Should().OnlyContain(p => ReferenceCredentialFiles.Holds(p));
        ReferenceCredentialFiles.Holds("a/.env.example.md").Should().BeTrue();
        ReferenceCredentialFiles.Holds("a/app.py").Should().BeFalse();
    }

    [Fact]
    public void ReferenceUploadLimits_Megabytes_StatesASizeAsARefusalReadsIt()
    {
        ReferenceUploadLimits.Megabytes(5L * 1024 * 1024).Should().Be("5 MB");
    }

    private static ReferenceUploadPart Part(string path) => new(path, Text("x"));
}
