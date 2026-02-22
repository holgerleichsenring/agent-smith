using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Services;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

public sealed class CodeMapGeneratorTests : IDisposable
{
    private readonly string _tempDir;

    public CodeMapGeneratorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "agentsmith-codemap-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void CollectDotNetInput_WithCsproj_IncludesProjectFiles()
    {
        var csprojContent = "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>";
        File.WriteAllText(Path.Combine(_tempDir, "Test.csproj"), csprojContent);

        var result = CodeMapGenerator.CollectArchitectureInput(
            CreateProject("C#"), _tempDir);

        result.Should().Contain("Test.csproj");
        result.Should().Contain("net8.0");
    }

    [Fact]
    public void CollectDotNetInput_WithInterfaces_IncludesInterfaceFiles()
    {
        var contractsDir = Path.Combine(_tempDir, "Contracts");
        Directory.CreateDirectory(contractsDir);
        File.WriteAllText(Path.Combine(contractsDir, "IMyService.cs"), "public interface IMyService {}");

        var result = CodeMapGenerator.CollectDotNetInput(_tempDir);

        result.Should().Contain("IMyService.cs");
    }

    [Fact]
    public void CollectDotNetInput_ExcludesObjBin()
    {
        var objDir = Path.Combine(_tempDir, "obj");
        Directory.CreateDirectory(objDir);
        File.WriteAllText(Path.Combine(objDir, "Generated.cs"), "// generated");

        var binDir = Path.Combine(_tempDir, "bin");
        Directory.CreateDirectory(binDir);
        File.WriteAllText(Path.Combine(binDir, "Output.cs"), "// output");

        File.WriteAllText(Path.Combine(_tempDir, "Real.cs"), "// real code");

        var result = CodeMapGenerator.CollectDotNetInput(_tempDir);

        result.Should().Contain("Real.cs");
        result.Should().NotContain("Generated.cs");
        result.Should().NotContain("Output.cs");
    }

    [Fact]
    public void CollectTypeScriptInput_WithPackageJson_IncludesContent()
    {
        File.WriteAllText(Path.Combine(_tempDir, "package.json"),
            """{"name": "test-app", "version": "1.0.0"}""");

        var result = CodeMapGenerator.CollectTypeScriptInput(_tempDir);

        result.Should().Contain("package.json");
        result.Should().Contain("test-app");
    }

    [Fact]
    public void CollectTypeScriptInput_WithServiceFiles_IncludesServiceFiles()
    {
        File.WriteAllText(Path.Combine(_tempDir, "user-service.ts"), "export class UserService {}");

        var result = CodeMapGenerator.CollectTypeScriptInput(_tempDir);

        result.Should().Contain("user-service.ts");
    }

    [Fact]
    public void CollectPythonInput_WithInitFiles_IncludesModuleExports()
    {
        var pkgDir = Path.Combine(_tempDir, "mypackage");
        Directory.CreateDirectory(pkgDir);
        File.WriteAllText(Path.Combine(pkgDir, "__init__.py"), "from .core import MyClass");

        var result = CodeMapGenerator.CollectPythonInput(_tempDir);

        result.Should().Contain("__init__.py");
        result.Should().Contain("MyClass");
    }

    [Fact]
    public void BuildUserPrompt_IncludesLanguageAndRuntime()
    {
        var project = CreateProject("C#");

        var result = CodeMapGenerator.BuildUserPrompt(project, "arch input", "tree");

        result.Should().Contain("C#");
        result.Should().Contain(".NET 8");
    }

    [Fact]
    public void BuildUserPrompt_IncludesOutputFormat()
    {
        var project = CreateProject("Python");

        var result = CodeMapGenerator.BuildUserPrompt(project, "arch", "tree");

        result.Should().Contain("modules:");
        result.Should().Contain("entry_points:");
        result.Should().Contain("dependency_graph:");
    }

    [Fact]
    public void IsValidYaml_ValidYaml_ReturnsTrue()
    {
        var yaml = "modules:\n  - name: Core\n    path: src/Core";

        CodeMapGenerator.IsValidYaml(yaml).Should().BeTrue();
    }

    [Fact]
    public void IsValidYaml_InvalidYaml_ReturnsFalse()
    {
        var yaml = "{{invalid: [yaml";

        CodeMapGenerator.IsValidYaml(yaml).Should().BeFalse();
    }

    [Fact]
    public void IsValidYaml_EmptyString_ReturnsFalse()
    {
        CodeMapGenerator.IsValidYaml("").Should().BeFalse();
        CodeMapGenerator.IsValidYaml("  ").Should().BeFalse();
    }

    [Fact]
    public void GenerateTree_ExcludesGitAndNodeModules()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, ".git"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "node_modules"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "src"));
        File.WriteAllText(Path.Combine(_tempDir, "src", "app.ts"), "");

        var result = CodeMapGenerator.GenerateTree(_tempDir, 3);

        result.Should().Contain("src/");
        result.Should().NotContain(".git");
        result.Should().NotContain("node_modules");
    }

    [Fact]
    public void TryReadFileTruncated_LargeFile_Truncates()
    {
        var largePath = Path.Combine(_tempDir, "large.txt");
        File.WriteAllText(largePath, new string('x', 5000));

        var result = CodeMapGenerator.TryReadFileTruncated(largePath);

        result.Should().NotBeNull();
        result!.Should().Contain("(truncated)");
        result.Length.Should().BeLessThan(5000);
    }

    [Fact]
    public void TryReadFileTruncated_SmallFile_ReturnsFullContent()
    {
        var smallPath = Path.Combine(_tempDir, "small.txt");
        File.WriteAllText(smallPath, "hello world");

        var result = CodeMapGenerator.TryReadFileTruncated(smallPath);

        result.Should().Be("hello world");
    }

    [Fact]
    public void StripCodeFences_RemovesYamlFences()
    {
        var input = "```yaml\nmodules:\n  - name: Core\n```";

        var result = CodeMapGenerator.StripCodeFences(input);

        result.Should().Be("modules:\n  - name: Core");
    }

    [Fact]
    public void StripCodeFences_RemovesGenericFences()
    {
        var input = "```\nmodules:\n  - name: Core\n```";

        var result = CodeMapGenerator.StripCodeFences(input);

        result.Should().Be("modules:\n  - name: Core");
    }

    [Fact]
    public void StripCodeFences_PlainYaml_ReturnsAsIs()
    {
        var input = "modules:\n  - name: Core";

        var result = CodeMapGenerator.StripCodeFences(input);

        result.Should().Be(input);
    }

    [Fact]
    public void CollectArchitectureInput_UnknownLanguage_ReturnsGenericTree()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, "src"));
        File.WriteAllText(Path.Combine(_tempDir, "src", "main.rs"), "fn main() {}");

        var result = CodeMapGenerator.CollectArchitectureInput(
            CreateProject("Rust"), _tempDir);

        result.Should().Contain("Directory listing");
    }

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
