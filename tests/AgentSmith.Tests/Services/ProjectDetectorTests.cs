using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services;
using AgentSmith.Infrastructure.Core.Services.Detection;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services;

public class ProjectDetectorTests
{
    private static readonly ILanguageDetector[] Detectors =
    [
        new DotNetLanguageDetector(NullLogger<DotNetLanguageDetector>.Instance),
        new TypeScriptLanguageDetector(NullLogger<TypeScriptLanguageDetector>.Instance),
        new PythonLanguageDetector()
    ];

    private readonly ProjectDetector _sut = new(Detectors, NullLogger<ProjectDetector>.Instance);

    [Fact]
    public async Task Detect_DotNetProject_ReturnsCorrectLanguageAndRuntime()
    {
        var fs = new FakeFs("/work");
        fs.AddFile("/work/MyApp.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Anthropic.SDK" Version="5.9.0" />
                <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
                <PackageReference Include="xunit" Version="2.6.2" />
              </ItemGroup>
            </Project>
            """);

        var result = await _sut.DetectAsync(fs.Reader.Object, "/work", CancellationToken.None);

        result.Language.Should().Be("C#");
        result.Runtime.Should().Contain(".NET");
        result.PackageManager.Should().Be("NuGet");
        result.BuildCommand.Should().Be("dotnet build");
        result.TestCommand.Should().Be("dotnet test");
        result.Sdks.Should().Contain("Anthropic.SDK");
    }

    [Fact]
    public async Task Detect_TypeScriptProject_WithPnpm_DetectsCorrectPackageManager()
    {
        var fs = new FakeFs("/work");
        fs.AddFile("/work/package.json", """
            {
              "name": "my-app",
              "scripts": {
                "build": "vite build",
                "test": "vitest"
              },
              "dependencies": {
                "react": "^18.0.0"
              },
              "devDependencies": {
                "vitest": "^1.0.0",
                "typescript": "^5.0.0"
              }
            }
            """);
        fs.AddFile("/work/tsconfig.json", "{}");
        fs.AddFile("/work/pnpm-lock.yaml", "lockfileVersion: 9");

        var result = await _sut.DetectAsync(fs.Reader.Object, "/work", CancellationToken.None);

        result.Language.Should().Be("TypeScript");
        result.PackageManager.Should().Be("pnpm");
        result.BuildCommand.Should().Be("pnpm run build");
        result.TestCommand.Should().Be("pnpm test");
        result.Sdks.Should().Contain("react");
    }

    [Fact]
    public async Task Detect_PythonProject_WithPoetry_DetectsCorrectPackageManager()
    {
        var fs = new FakeFs("/work");
        fs.AddFile("/work/pyproject.toml", """
            [tool.poetry]
            name = "my-app"
            version = "0.1.0"

            [tool.pytest.ini_options]
            testpaths = ["tests"]
            """);

        var result = await _sut.DetectAsync(fs.Reader.Object, "/work", CancellationToken.None);

        result.Language.Should().Be("Python");
        result.PackageManager.Should().Be("poetry");
        result.TestCommand.Should().Be("pytest");
    }

    [Fact]
    public async Task Detect_PythonProject_WithUv_DetectsCorrectPackageManager()
    {
        var fs = new FakeFs("/work");
        fs.AddFile("/work/pyproject.toml", """
            [project]
            name = "my-app"
            """);
        fs.AddFile("/work/uv.lock", "version = 1");

        var result = await _sut.DetectAsync(fs.Reader.Object, "/work", CancellationToken.None);

        result.Language.Should().Be("Python");
        result.PackageManager.Should().Be("uv");
    }

    [Fact]
    public async Task Detect_DockerAndGitHubActions_DetectsInfrastructure()
    {
        var fs = new FakeFs("/work");
        fs.AddFile("/work/package.json", """{"name":"test"}""");
        fs.AddFile("/work/Dockerfile", "FROM node:20");
        fs.AddDir("/work/.github");
        fs.AddDir("/work/.github/workflows");
        fs.AddFile("/work/.github/workflows/ci.yml", "name: CI");

        var result = await _sut.DetectAsync(fs.Reader.Object, "/work", CancellationToken.None);

        result.Infrastructure.Should().Contain("Docker");
        result.Infrastructure.Should().Contain("GitHub-Actions");
    }

    [Fact]
    public async Task Detect_NoMarkers_ReturnsUnknown()
    {
        var fs = new FakeFs("/work");

        var result = await _sut.DetectAsync(fs.Reader.Object, "/work", CancellationToken.None);

        result.Language.Should().Be("Unknown");
        result.Runtime.Should().BeNull();
        result.BuildCommand.Should().BeNull();
        result.TestCommand.Should().BeNull();
    }

    [Fact]
    public async Task Detect_JavaScriptProject_WithYarn_DetectsCorrectly()
    {
        var fs = new FakeFs("/work");
        fs.AddFile("/work/package.json", """
            {
              "name": "my-app",
              "scripts": { "build": "webpack", "test": "jest" }
            }
            """);
        fs.AddFile("/work/yarn.lock", "# yarn lockfile v1");

        var result = await _sut.DetectAsync(fs.Reader.Object, "/work", CancellationToken.None);

        result.Language.Should().Be("JavaScript");
        result.PackageManager.Should().Be("yarn");
        result.BuildCommand.Should().Be("yarn run build");
        result.TestCommand.Should().Be("yarn test");
    }

    [Fact]
    public async Task Detect_ReadsReadmeExcerpt()
    {
        var fs = new FakeFs("/work");
        fs.AddFile("/work/package.json", """{"name":"test"}""");
        fs.AddFile("/work/README.md",
            "# My Project\n\nThis is a test project that does interesting things.");

        var result = await _sut.DetectAsync(fs.Reader.Object, "/work", CancellationToken.None);

        result.ReadmeExcerpt.Should().Contain("My Project");
    }

    [Fact]
    public async Task Detect_DotNetProject_CollectsKeyFiles()
    {
        var fs = new FakeFs("/work");
        fs.AddFile("/work/App.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        var result = await _sut.DetectAsync(fs.Reader.Object, "/work", CancellationToken.None);

        result.KeyFiles.Should().Contain("App.csproj");
    }

    [Fact]
    public async Task Detect_KubernetesAndTerraform_DetectsInfrastructure()
    {
        var fs = new FakeFs("/work");
        fs.AddFile("/work/package.json", """{"name":"test"}""");
        fs.AddDir("/work/k8s");
        fs.AddDir("/work/terraform");

        var result = await _sut.DetectAsync(fs.Reader.Object, "/work", CancellationToken.None);

        result.Infrastructure.Should().Contain("K8s");
        result.Infrastructure.Should().Contain("Terraform");
    }

    /// <summary>
    /// Lightweight in-memory file system mock for ISandboxFileReader. Tracks files
    /// and directories so ListAsync/ExistsAsync/TryReadAsync return consistent results.
    /// </summary>
    private sealed class FakeFs
    {
        private readonly HashSet<string> _dirs = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);

        public Mock<ISandboxFileReader> Reader { get; } = new();

        public FakeFs(string root)
        {
            _dirs.Add(root);
            Reader.Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns<string, CancellationToken>((p, _) => Task.FromResult(_files.ContainsKey(p) || _dirs.Contains(p)));
            Reader.Setup(r => r.TryReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns<string, CancellationToken>((p, _) =>
                    Task.FromResult(_files.TryGetValue(p, out var c) ? c : null));
            Reader.Setup(r => r.ReadRequiredAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns<string, CancellationToken>((p, _) => Task.FromResult(_files[p]));
            Reader.Setup(r => r.ListAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
                .Returns<string, int?, CancellationToken>((p, _, _) => Task.FromResult(ListUnder(p)));
        }

        public void AddDir(string path) => _dirs.Add(path);

        public void AddFile(string path, string content)
        {
            _files[path] = content;
            // Add parent directories
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
