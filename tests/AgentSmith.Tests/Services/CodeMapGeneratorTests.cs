using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Services;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

public sealed class CodeMapGeneratorTests
{
    [Fact]
    public void BuildUserPrompt_IncludesLanguageAndRuntime()
    {
        var project = CreateProject("C#");
        var snapshot = CreateEmptySnapshot();

        var result = CodeMapGenerator.BuildUserPrompt(project, snapshot);

        result.Should().Contain("C#");
        result.Should().Contain(".NET 8");
    }

    [Fact]
    public void BuildUserPrompt_IncludesOutputFormat()
    {
        var project = CreateProject("Python");
        var snapshot = CreateEmptySnapshot();

        var result = CodeMapGenerator.BuildUserPrompt(project, snapshot);

        result.Should().Contain("modules:");
        result.Should().Contain("entry_points:");
        result.Should().Contain("dependency_graph:");
    }

    [Fact]
    public void BuildUserPrompt_IncludesFrameworks()
    {
        var project = CreateProject("C#");
        var snapshot = CreateEmptySnapshot();

        var result = CodeMapGenerator.BuildUserPrompt(project, snapshot);

        result.Should().Contain("WebApi");
    }

    [Fact]
    public void BuildUserPrompt_IncludesDirectoryTree()
    {
        var project = CreateProject("C#");
        var snapshot = new RepoSnapshot(
            ConfigFileContents: [],
            CodeSamples: [],
            DirectoryTree: "src/\n  Program.cs\ntests/\n  Tests.cs");

        var result = CodeMapGenerator.BuildUserPrompt(project, snapshot);

        result.Should().Contain("src/");
        result.Should().Contain("Program.cs");
        result.Should().Contain("tests/");
    }

    [Fact]
    public void BuildUserPrompt_IncludesCodeSamples()
    {
        var project = CreateProject("C#");
        var snapshot = new RepoSnapshot(
            ConfigFileContents: [],
            CodeSamples: ["### src/Service.cs\npublic class Service {}"],
            DirectoryTree: "");

        var result = CodeMapGenerator.BuildUserPrompt(project, snapshot);

        result.Should().Contain("Code Samples");
        result.Should().Contain("Service.cs");
    }

    [Fact]
    public void BuildUserPrompt_IncludesConfigFiles()
    {
        var project = CreateProject("C#");
        var snapshot = new RepoSnapshot(
            ConfigFileContents: ["### .editorconfig\n```\nroot = true\n```"],
            CodeSamples: [],
            DirectoryTree: "");

        var result = CodeMapGenerator.BuildUserPrompt(project, snapshot);

        result.Should().Contain("Config Files");
        result.Should().Contain(".editorconfig");
    }

    [Fact]
    public void BuildUserPrompt_OmitsCodeSamplesWhenEmpty()
    {
        var project = CreateProject("C#");
        var snapshot = CreateEmptySnapshot();

        var result = CodeMapGenerator.BuildUserPrompt(project, snapshot);

        result.Should().NotContain("Code Samples");
    }

    [Fact]
    public void BuildUserPrompt_OmitsConfigFilesWhenEmpty()
    {
        var project = CreateProject("C#");
        var snapshot = CreateEmptySnapshot();

        var result = CodeMapGenerator.BuildUserPrompt(project, snapshot);

        result.Should().NotContain("Config Files");
    }

    private static RepoSnapshot CreateEmptySnapshot() =>
        new(ConfigFileContents: [], CodeSamples: [], DirectoryTree: "");

    private static DetectedProject CreateProject(string language) =>
        new(
            Language: language,
            Runtime: language == "C#" ? ".NET 8" : language == "Python" ? "Python" : "Node.js",
            PackageManager: "unknown",
            BuildCommand: "build",
            TestCommand: "test",
            Frameworks: ["WebApi"],
            Infrastructure: [],
            KeyFiles: [],
            Sdks: []);
}
