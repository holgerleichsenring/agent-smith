using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services;

public class RepoSnapshotCollectorTests
{
    private readonly RepoSnapshotCollector _sut = new(NullLogger<RepoSnapshotCollector>.Instance);

    [Fact]
    public async Task Collect_EditorConfig_ReadsContent()
    {
        var fs = new FakeFs();
        fs.AddFile("/work/.editorconfig", "root = true\n\n[*]\nindent_style = space\nindent_size = 4");

        var snapshot = await _sut.CollectAsync(fs.Reader.Object, "/work", CreateProject("C#"), CancellationToken.None);

        snapshot.ConfigFileContents.Should().ContainSingle();
        snapshot.ConfigFileContents[0].Should().Contain(".editorconfig");
        snapshot.ConfigFileContents[0].Should().Contain("indent_style = space");
    }

    [Fact]
    public async Task Collect_EslintConfig_ReadsContent()
    {
        var fs = new FakeFs();
        fs.AddFile("/work/.eslintrc.json", """{"extends": "eslint:recommended"}""");

        var snapshot = await _sut.CollectAsync(fs.Reader.Object, "/work", CreateProject("TypeScript"), CancellationToken.None);

        snapshot.ConfigFileContents.Should().ContainSingle();
        snapshot.ConfigFileContents[0].Should().Contain(".eslintrc.json");
        snapshot.ConfigFileContents[0].Should().Contain("eslint:recommended");
    }

    [Fact]
    public async Task Collect_MultipleConfigFiles_ReadsAll()
    {
        var fs = new FakeFs();
        fs.AddFile("/work/.editorconfig", "root = true");
        fs.AddFile("/work/.prettierrc", """{"semi": true}""");

        var snapshot = await _sut.CollectAsync(fs.Reader.Object, "/work", CreateProject("TypeScript"), CancellationToken.None);

        snapshot.ConfigFileContents.Should().HaveCount(2);
    }

    [Fact]
    public async Task Collect_CodeSamples_CollectsLargestFiles()
    {
        var fs = new FakeFs();
        fs.AddFile("/work/src/Small.cs", "class Small {}");
        fs.AddFile("/work/src/Large.cs",
            string.Join('\n', Enumerable.Range(1, 50).Select(i => $"// Line {i}")));

        var snapshot = await _sut.CollectAsync(fs.Reader.Object, "/work", CreateProject("C#"), CancellationToken.None);

        snapshot.CodeSamples.Should().HaveCount(2);
    }

    [Fact]
    public async Task Collect_EmptyRepo_ReturnsEmptySnapshot()
    {
        var fs = new FakeFs();

        var snapshot = await _sut.CollectAsync(fs.Reader.Object, "/work", CreateProject("C#"), CancellationToken.None);

        snapshot.ConfigFileContents.Should().BeEmpty();
        snapshot.CodeSamples.Should().BeEmpty();
    }

    [Fact]
    public async Task Collect_ExcludesNodeModules()
    {
        var fs = new FakeFs();
        fs.AddFile("/work/node_modules/pkg/index.js", "module.exports = {}");
        fs.AddFile("/work/src/app.js", "const app = require('express')();");

        var snapshot = await _sut.CollectAsync(fs.Reader.Object, "/work", CreateProject("JavaScript"), CancellationToken.None);

        snapshot.CodeSamples.Should().ContainSingle();
        snapshot.CodeSamples[0].Should().Contain("app.js");
    }

    [Fact]
    public async Task CollectConfigFilesAsync_Static_ReturnsConfigContents()
    {
        var fs = new FakeFs();
        fs.AddFile("/work/.editorconfig", "root = true");

        var result = await RepoSnapshotCollector.CollectConfigFilesAsync(fs.Reader.Object, "/work", CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Should().Contain("root = true");
    }

    [Fact]
    public async Task CollectCodeSamplesAsync_Static_LanguageFiltering()
    {
        var fs = new FakeFs();
        fs.AddFile("/work/src/app.py", "print('hello')");
        fs.AddFile("/work/src/utils.cs", "class Utils {}");

        var result = await RepoSnapshotCollector.CollectCodeSamplesAsync(
            fs.Reader.Object, "/work", CreateProject("Python"), CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Should().Contain("app.py");
    }

    [Fact]
    public async Task Collect_IncludesDirectoryTree()
    {
        var fs = new FakeFs();
        fs.AddDir("/work/src");
        fs.AddFile("/work/src/app.cs", "class App {}");
        fs.AddFile("/work/README.md", "# Test");

        var snapshot = await _sut.CollectAsync(fs.Reader.Object, "/work", CreateProject("C#"), CancellationToken.None);

        snapshot.DirectoryTree.Should().Contain("src");
        snapshot.DirectoryTree.Should().Contain("README.md");
        snapshot.DirectoryTree.Should().Contain("app.cs");
    }

    [Fact]
    public async Task GenerateTreeAsync_EmptyDir_ReturnsEmpty()
    {
        var fs = new FakeFs();

        var result = await RepoSnapshotCollector.GenerateTreeAsync(fs.Reader.Object, "/work", 3, CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateTreeAsync_WithFiles_ReturnsStructure()
    {
        var fs = new FakeFs();
        fs.AddFile("/work/README.md", "# Test");
        fs.AddDir("/work/src");
        fs.AddFile("/work/src/main.cs", "class Main {}");

        var result = await RepoSnapshotCollector.GenerateTreeAsync(fs.Reader.Object, "/work", 3, CancellationToken.None);

        result.Should().Contain("README.md");
        result.Should().Contain("src");
        result.Should().Contain("main.cs");
    }

    [Fact]
    public async Task GenerateTreeAsync_ExcludesGitAndNodeModules()
    {
        var fs = new FakeFs();
        fs.AddDir("/work/.git");
        fs.AddDir("/work/node_modules");
        fs.AddDir("/work/src");

        var result = await RepoSnapshotCollector.GenerateTreeAsync(fs.Reader.Object, "/work", 3, CancellationToken.None);

        result.Should().Contain("src");
        result.Should().NotContain(".git");
        result.Should().NotContain("node_modules");
    }

    private static DetectedProject CreateProject(string language) =>
        new(
            Language: language,
            Runtime: language == "C#" ? ".NET 8" : "Node.js",
            PackageManager: "npm",
            BuildCommand: "build",
            TestCommand: "test",
            Frameworks: [],
            Infrastructure: [],
            KeyFiles: [],
            Sdks: []);

    /// <summary>
    /// Simple in-memory ISandboxFileReader mock for snapshot testing.
    /// </summary>
    private sealed class FakeFs
    {
        private readonly HashSet<string> _dirs = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);

        public Mock<ISandboxFileReader> Reader { get; } = new();

        public FakeFs()
        {
            Reader.Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns<string, CancellationToken>((p, _) => Task.FromResult(_files.ContainsKey(p) || _dirs.Contains(p)));
            Reader.Setup(r => r.TryReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns<string, CancellationToken>((p, _) =>
                    Task.FromResult(_files.TryGetValue(p, out var c) ? c : null));
            Reader.Setup(r => r.ListAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
                .Returns<string, int?, CancellationToken>((p, _, _) => Task.FromResult(ListUnder(p)));
        }

        public void AddDir(string path) => _dirs.Add(path);

        public void AddFile(string path, string content)
        {
            _files[path] = content;
            var dir = Path.GetDirectoryName(path);
            while (!string.IsNullOrEmpty(dir))
            {
                _dirs.Add(dir);
                dir = Path.GetDirectoryName(dir);
            }
        }

        private IReadOnlyList<string> ListUnder(string root)
        {
            var prefix = root.EndsWith('/') ? root : root + "/";
            var result = new List<string>();
            foreach (var dir in _dirs)
            {
                if (dir.StartsWith(prefix, StringComparison.Ordinal) && dir != root)
                    result.Add(dir);
            }
            foreach (var file in _files.Keys)
            {
                if (file.StartsWith(prefix, StringComparison.Ordinal))
                    result.Add(file);
            }
            return result;
        }
    }
}
