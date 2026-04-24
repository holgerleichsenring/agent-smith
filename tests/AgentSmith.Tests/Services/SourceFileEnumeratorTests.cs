using AgentSmith.Infrastructure.Services.Security;
using FluentAssertions;
using LibGit2Sharp;

namespace AgentSmith.Tests.Services;

public sealed class SourceFileEnumeratorTests : IDisposable
{
    private readonly string _tempDir;

    public SourceFileEnumeratorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "SourceFileEnum_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best effort cleanup */ }
    }

    private void InitGitRepo()
    {
        Repository.Init(_tempDir);
    }

    private void Write(string relativePath, string content = "content")
    {
        var full = Path.Combine(_tempDir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }

    [Fact]
    public void GitRepo_GitignoredFile_NotYielded()
    {
        InitGitRepo();
        Write(".gitignore", "build/\n");
        Write("src/App.cs", "class App {}");
        Write("build/Generated.cs", "class Generated {}");

        var files = SourceFileEnumerator.EnumerateSourceFiles(_tempDir).ToList();

        files.Should().Contain(f => f.EndsWith("App.cs"));
        files.Should().NotContain(f => f.Contains("Generated.cs"));
    }

    [Fact]
    public void GitRepo_TrackedFile_Yielded()
    {
        InitGitRepo();
        Write(".gitignore", "build/\n");
        Write("src/Foo.cs", "class Foo {}");

        var files = SourceFileEnumerator.EnumerateSourceFiles(_tempDir).ToList();

        files.Should().Contain(f => f.EndsWith("Foo.cs"));
    }

    [Fact]
    public void NotGitRepo_FallsBackToHardcodedExcludes()
    {
        // No git init — hardcoded ExcludedDirectories must still apply
        Write("src/App.cs");
        Write("node_modules/junk.js");
        Write("bin/compiled.dll.meta");

        var files = SourceFileEnumerator.EnumerateSourceFiles(_tempDir).ToList();

        files.Should().Contain(f => f.EndsWith("App.cs"));
        files.Should().NotContain(f => f.Contains("node_modules"));
        files.Should().NotContain(f => f.Contains(Path.Combine(_tempDir, "bin")));
    }

    [Fact]
    public void GitRepo_NestedGitignore_Respected()
    {
        InitGitRepo();
        Write(".gitignore", "");
        Write("src/.gitignore", "generated/\n");
        Write("src/App.cs");
        Write("src/generated/Schema.cs");

        var files = SourceFileEnumerator.EnumerateSourceFiles(_tempDir).ToList();

        files.Should().Contain(f => f.EndsWith("App.cs"));
        files.Should().NotContain(f => f.Contains("Schema.cs"));
    }

    [Fact]
    public void GitRepo_BinaryFile_StillFiltered()
    {
        InitGitRepo();
        Write("logo.png", "not-really-a-png");
        Write("App.cs", "class App {}");

        var files = SourceFileEnumerator.EnumerateSourceFiles(_tempDir).ToList();

        files.Should().Contain(f => f.EndsWith("App.cs"));
        files.Should().NotContain(f => f.EndsWith(".png"));
    }

    [Fact]
    public void GitRepo_SiteDirGitignored_NotYielded()
    {
        // Mirrors the real agent-smith situation: site/ is gitignored but used to leak in
        InitGitRepo();
        Write(".gitignore", "site/\n");
        Write("src/App.cs");
        Write("site/index.html");
        Write("site/assets/javascripts/bundle.js");

        var files = SourceFileEnumerator.EnumerateSourceFiles(_tempDir).ToList();

        files.Should().Contain(f => f.EndsWith("App.cs"));
        files.Should().NotContain(f => f.Contains($"{Path.DirectorySeparatorChar}site{Path.DirectorySeparatorChar}"));
    }
}

public sealed class GitIgnoreResolverTests : IDisposable
{
    private readonly string _tempDir;

    public GitIgnoreResolverTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "GitIgnoreRes_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best effort cleanup */ }
    }

    [Fact]
    public void NonGitPath_IsNotRepo_AndNeverIgnored()
    {
        using var resolver = new GitIgnoreResolver(_tempDir);

        resolver.IsGitRepo.Should().BeFalse();
        resolver.IsIgnored(Path.Combine(_tempDir, "anything.txt")).Should().BeFalse();
    }

    [Fact]
    public void GitRepo_IsRecognized()
    {
        Repository.Init(_tempDir);
        File.WriteAllText(Path.Combine(_tempDir, ".gitignore"), "secret.txt\n");

        using var resolver = new GitIgnoreResolver(_tempDir);

        resolver.IsGitRepo.Should().BeTrue();
        resolver.IsIgnored(Path.Combine(_tempDir, "secret.txt")).Should().BeTrue();
        resolver.IsIgnored(Path.Combine(_tempDir, "open.txt")).Should().BeFalse();
    }
}
