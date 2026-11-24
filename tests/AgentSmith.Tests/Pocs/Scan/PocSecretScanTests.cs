using System.Text;
using AgentSmith.Contracts.Sandbox;
using FluentAssertions;

namespace AgentSmith.Tests.Pocs.Scan;

/// <summary>
/// 2026-11-23-933dc: the in-process pattern scan reads every text file in full and applies no ignore rule of the
/// upload; it is strict for the 'secrets' catalog (none loaded, one invalid, a timeout fail it) and lenient for the others
/// (a timeout is a note). Snippets mask every matched span on their line and show a window around their own.
/// </summary>
public sealed class PocSecretScanTests : IDisposable
{
    private const string Key = "AKIAABCDEFGHIJKLMNOP";
    private readonly List<PocPatternsDirectory> _dirs = [];

    [Fact]
    public void Scan_FindsAKeyWhereTheSecurityScanWouldSkip()
    {
        var deep = string.Join('/', Enumerable.Range(0, 15).Select(i => $"d{i}")) + "/deep.py";
        var big = new string('x', 1024 * 1024 + 10) + "\n" + new string('y', 4 * 1024 * 1024) + $"\nkey = '{Key}'\n";
        var result = Scan(File("README.md", $"key {Key}"), File("bin/config.py", Key), File(".gitignore", "secret.py\n"),
            File("secret.py", Key), File(deep, Key), File("big.txt", big));

        result.Failure.Should().BeNull();
        result.Hits.Select(h => h.File).Should().BeEquivalentTo(["README.md", "bin/config.py", "secret.py", deep, "big.txt"]);
        result.Hits.Single(h => h.File == "big.txt").Line.Should().Be(3);
    }

    [Fact]
    public void Scan_TwoKeysOnOneLine_AreTwoHits_AndBinaryFilesAreOneNote()
    {
        var result = Scan(File("a.py", $"x = '{Key}' + '{Key}'"), new ReferenceSetFile("logo.png", [0x89, 0x50, 0x00, 0x01]));

        result.Hits.Should().HaveCount(2);
        result.Notes.Should().ContainSingle().Which.Detail.Should().Contain("logo.png");
    }

    [Fact]
    public void Scan_TheSnippetMasksEveryMatchOnItsLine_NeverARawValue()
    {
        var result = Scan(File("src/config.py", $"key = \"{Key}\"; password = \"hunter2hunter2\""));

        result.Hits.Should().HaveCount(2);
        result.Hits.Should().OnlyContain(h => !h.MaskedSnippet.Contains(Key) && !h.MaskedSnippet.Contains("hunter2hunter2"));
        result.Hits.Single(h => h.PatternId == "generic-secret-assignment").MaskedSnippet.Should().Contain("<secret at src/config.py:1>");
    }

    [Fact]
    public void Scan_ALongMatchIsMaskedWhole_AndAOneLineFileShowsOnlyAWindow()
    {
        var long300 = "AIza" + new string('a', 35);
        var minified = new string('q', 2 * 1024 * 1024) + Key + new string('z', 1000);
        var result = Scan(File("a.js", minified), File("b.py", $"token = \"{new string('t', 300)}\" {long300}"));

        var window = result.Hits.Single(h => h.File == "a.js").MaskedSnippet;
        window.Length.Should().BeLessThan(80 + 80 + 40);
        result.Hits.Where(h => h.File == "b.py").Should().OnlyContain(h => !h.MaskedSnippet.Contains("ttttt") && !h.MaskedSnippet.Contains(long300));
    }

    [Fact]
    public void Scan_NoSecretPatterns_FailsNamingTheDirectory() =>
        Scan(new PocPatternsDirectory("name: secrets\npatterns: []\n"), File("a.py", Key)).Failure.Should().Contain("no secret patterns loaded from");

    [Fact]
    public void Scan_AnInvalidSecretRegex_FailsNamingItsId() =>
        Scan(new PocPatternsDirectory(PocPatternsDirectory.Secrets + "\n  - id: broken-one\n    regex: \"(unclosed\"\n    severity: high\n    confidence: 9\n    title: b\n    description: b\n"),
            File("a.py", "x")).Failure.Should().Contain("'broken-one'");

    [Fact]
    public void Scan_ASecretPatternTimeout_FailsTheScan()
    {
        var dir = new PocPatternsDirectory(PocPatternsDirectory.Secrets + Slow("slow-secret"));

        Scan(dir, File("a.py", Backtracking())).Failure.Should().Contain("'slow-secret' timed out on a.py");
    }

    [Fact]
    public void Scan_AnotherCatalogsTimeout_IsANote_AndTheScanGoesOn()
    {
        var dir = new PocPatternsDirectory(PocPatternsDirectory.Secrets, ("injection", "name: injection\npatterns:" + Slow("slow-injection")));

        var result = Scan(dir, File("bundle.min.js", Backtracking() + $" {Key}"));

        result.Failure.Should().BeNull();
        result.Notes.Should().ContainSingle(n => n.Title.Contains("slow-injection") && n.File == "bundle.min.js");
        result.Hits.Should().Contain(h => h.PatternId == "aws-access-key");
    }

    private AgentSmith.Contracts.Pocs.PocSecretScanResult Scan(params ReferenceSetFile[] files) => Scan(new PocPatternsDirectory(), files);

    private AgentSmith.Contracts.Pocs.PocSecretScanResult Scan(PocPatternsDirectory dir, params ReferenceSetFile[] files)
    {
        _dirs.Add(dir);
        return dir.Scan().Scan(files);
    }

    private static ReferenceSetFile File(string path, string text) => new(path, Encoding.UTF8.GetBytes(text));

    private static string Slow(string id) =>
        $"\n  - id: {id}\n    regex: \"^(a+)+$\"\n    severity: critical\n    confidence: 9\n    title: {id}\n    description: {id}\n";

    private static string Backtracking() => new string('a', 5000) + "!";

    public void Dispose()
    {
        foreach (var dir in _dirs) dir.Dispose();
    }
}
