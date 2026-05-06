using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Core.Services;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Services;

public class ContextGeneratorTests
{

    [Fact]
    public void BuildUserPrompt_IncludesDetectedStack()
    {
        var project = CreateDetectedProject("C#", ".NET 8", "NuGet");
        var snapshot = CreateEmptySnapshot();

        var result = NewBuilder().Build(project, "", snapshot);

        result.Should().Contain("C#");
        result.Should().Contain(".NET 8");
        result.Should().Contain("NuGet");
    }

    [Fact]
    public void BuildUserPrompt_IncludesReadmeExcerpt()
    {
        var project = CreateDetectedProject("Python", "Python", "pip",
            readmeExcerpt: "This is a Django web application.");
        var snapshot = CreateEmptySnapshot();

        var result = NewBuilder().Build(project, "", snapshot);

        result.Should().Contain("README (excerpt)");
        result.Should().Contain("Django web application");
    }

    [Fact]
    public void BuildUserPrompt_OmitsReadmeSectionWhenNull()
    {
        var project = CreateDetectedProject("TypeScript", "Node.js", "npm");
        var snapshot = CreateEmptySnapshot();

        var result = NewBuilder().Build(project, "", snapshot);

        result.Should().NotContain("README (excerpt)");
    }

    [Fact]
    public async Task ReadKeyFilesAsync_TruncatesLargeFiles()
    {
        var fileName = "large.csproj";
        var reader = new Mock<ISandboxFileReader>();
        reader.Setup(r => r.TryReadAsync("/work/large.csproj", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new string('x', 5000));

        var result = await ContextGenerator.ReadKeyFilesAsync(
            reader.Object, [fileName], "/work", CancellationToken.None);

        result.Should().Contain("(truncated)");
        result.Should().Contain(fileName);
    }

    [Fact]
    public async Task ReadKeyFilesAsync_SkipsMissingFiles()
    {
        var reader = new Mock<ISandboxFileReader>();
        reader.Setup(r => r.TryReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var result = await ContextGenerator.ReadKeyFilesAsync(
            reader.Object, ["nonexistent.txt"], "/work", CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadKeyFilesAsync_ReadsExistingFile()
    {
        var reader = new Mock<ISandboxFileReader>();
        reader.Setup(r => r.TryReadAsync("/work/package.json", It.IsAny<CancellationToken>()))
            .ReturnsAsync("""{"name": "my-app"}""");

        var result = await ContextGenerator.ReadKeyFilesAsync(
            reader.Object, ["package.json"], "/work", CancellationToken.None);

        result.Should().Contain("package.json");
        result.Should().Contain("my-app");
    }

    [Fact]
    public void BuildUserPrompt_IncludesConfigFiles()
    {
        var project = CreateDetectedProject("C#", ".NET 8", "NuGet");
        var snapshot = new RepoSnapshot(
            ConfigFileContents: ["### .editorconfig\n```\nroot = true\nindent_style = space\n```"],
            CodeSamples: [],
            DirectoryTree: "");

        var result = NewBuilder().Build(project, "", snapshot);

        result.Should().Contain("Config Files");
        result.Should().Contain(".editorconfig");
        result.Should().Contain("indent_style = space");
    }

    [Fact]
    public void BuildUserPrompt_IncludesCodeSamples()
    {
        var project = CreateDetectedProject("C#", ".NET 8", "NuGet");
        var snapshot = new RepoSnapshot(
            ConfigFileContents: [],
            CodeSamples: ["### src/Program.cs\nusing System;\nConsole.WriteLine(\"Hello\");"],
            DirectoryTree: "");

        var result = NewBuilder().Build(project, "", snapshot);

        result.Should().Contain("Code Samples");
        result.Should().Contain("Program.cs");
        result.Should().Contain("Console.WriteLine");
    }

    [Fact]
    public void BuildUserPrompt_IncludesDirectoryTree()
    {
        var project = CreateDetectedProject("C#", ".NET 8", "NuGet");
        var snapshot = new RepoSnapshot(
            ConfigFileContents: [],
            CodeSamples: [],
            DirectoryTree: "src/\n  Program.cs\nREADME.md");

        var result = NewBuilder().Build(project, "", snapshot);

        result.Should().Contain("src/");
        result.Should().Contain("Program.cs");
        result.Should().Contain("README.md");
    }

    [Fact]
    public void BuildUserPrompt_UsesExtendedQualityTemplate()
    {
        var project = CreateDetectedProject("C#", ".NET 8", "NuGet");
        var snapshot = CreateEmptySnapshot();

        var result = NewBuilder().Build(project, "", snapshot);

        result.Should().Contain("detected-style");
        result.Should().Contain("architecture");
        result.Should().Contain("methodology");
        result.Should().Contain("quality-score");
    }

    [Fact]
    public void BuildSnapshotSection_EmptySnapshot_ReturnsEmpty()
    {
        var snapshot = CreateEmptySnapshot();

        NewBuilder().BuildSnapshotSection(snapshot).Should().BeEmpty();
    }

    [Fact]
    public void BuildSnapshotSection_WithConfigsAndSamples_IncludesBoth()
    {
        var snapshot = new RepoSnapshot(
            ConfigFileContents: ["### .editorconfig\n```\nroot = true\n```"],
            CodeSamples: ["### src/Main.cs\nclass Main {}"],
            DirectoryTree: "");

        var result = NewBuilder().BuildSnapshotSection(snapshot);

        result.Should().Contain("Config Files");
        result.Should().Contain("Code Samples");
        result.Should().Contain(".editorconfig");
        result.Should().Contain("Main.cs");
    }

    private const string QualityTemplate = """
            quality:
              lang: english-only
              detected-style:
                naming: { classes: <PascalCase|camelCase|snake_case>, variables: <camelCase|snake_case>, files: <pattern> }
              architecture:
                style: [<DDD|CleanArch|Hexagonal|MVC|Layered|ad-hoc>]
              methodology:
                testing: <test-first|test-after|no-tests>
              quality-score: <high|medium|low>
            """;

    private static ContextUserPromptBuilder NewBuilder() =>
        new(new FakePromptCatalog().WithPrompt("context-quality-template", QualityTemplate));

    private static RepoSnapshot CreateEmptySnapshot() =>
        new(ConfigFileContents: [], CodeSamples: [], DirectoryTree: "");

    private static DetectedProject CreateDetectedProject(
        string language, string runtime, string packageManager,
        string? readmeExcerpt = null) =>
        new(
            Language: language,
            Runtime: runtime,
            PackageManager: packageManager,
            BuildCommand: "build",
            TestCommand: "test",
            Frameworks: [],
            Infrastructure: [],
            KeyFiles: [],
            Sdks: [],
            ReadmeExcerpt: readmeExcerpt);
}
