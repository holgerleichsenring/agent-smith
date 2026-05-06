using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Services.Security;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Services;

public sealed class SourceFileEnumeratorHostTests : IDisposable
{
    private readonly string _tempDir;

    public SourceFileEnumeratorHostTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "SourceFileEnum_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best effort cleanup */ }
    }

    private void Write(string relativePath, string content = "content")
    {
        var full = Path.Combine(_tempDir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }

    [Fact]
    public void GitignoredFile_NotYielded()
    {
        Write(".gitignore", "build/\n");
        Write("src/App.cs", "class App {}");
        Write("build/Generated.cs", "class Generated {}");

        var files = SourceFileEnumerator.EnumerateSourceFiles(_tempDir).ToList();

        files.Should().Contain(f => f.EndsWith("App.cs"));
        files.Should().NotContain(f => f.Contains("Generated.cs"));
    }

    [Fact]
    public void NoGitignore_FallsBackToHardcodedExcludes()
    {
        Write("src/App.cs");
        Write("node_modules/junk.js");
        Write("bin/compiled.dll.meta");

        var files = SourceFileEnumerator.EnumerateSourceFiles(_tempDir).ToList();

        files.Should().Contain(f => f.EndsWith("App.cs"));
        files.Should().NotContain(f => f.Contains("node_modules"));
        files.Should().NotContain(f => f.Contains(Path.Combine(_tempDir, "bin")));
    }

    [Fact]
    public void BinaryExtension_Skipped()
    {
        Write("logo.png", "not-really-a-png");
        Write("App.cs", "class App {}");

        var files = SourceFileEnumerator.EnumerateSourceFiles(_tempDir).ToList();

        files.Should().Contain(f => f.EndsWith("App.cs"));
        files.Should().NotContain(f => f.EndsWith(".png"));
    }

    [Fact]
    public void GitignoredSiteDir_NotYielded()
    {
        Write(".gitignore", "site/\n");
        Write("src/App.cs");
        Write("site/index.html");
        Write("site/assets/javascripts/bundle.js");

        var files = SourceFileEnumerator.EnumerateSourceFiles(_tempDir).ToList();

        files.Should().Contain(f => f.EndsWith("App.cs"));
        files.Should().NotContain(f => f.Contains($"{Path.DirectorySeparatorChar}site{Path.DirectorySeparatorChar}"));
    }
}

public sealed class SourceFileEnumeratorSandboxTests
{
    [Fact]
    public async Task EnumerateAsync_RoutesThroughReader_AndAppliesGitignore()
    {
        var reader = new Mock<ISandboxFileReader>();
        reader.Setup(r => r.TryReadAsync("/work/.gitignore", It.IsAny<CancellationToken>()))
            .ReturnsAsync("build/\n");
        reader.Setup(r => r.ListAsync("/work", It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                "/work/.gitignore",
                "/work/src/App.cs",
                "/work/build/Generated.cs",
                "/work/node_modules/junk.js"
            });

        var files = new List<string>();
        await foreach (var f in SourceFileEnumerator.EnumerateAsync(reader.Object, "/work", CancellationToken.None))
            files.Add(f);

        files.Should().Contain("/work/src/App.cs");
        files.Should().NotContain(p => p.Contains("Generated.cs"));
        files.Should().NotContain(p => p.Contains("node_modules"));
    }
}
