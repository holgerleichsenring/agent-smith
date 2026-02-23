using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Providers.Agent;
using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using Microsoft.Extensions.Logging;
using YamlDotNet.RepresentationModel;

namespace AgentSmith.Infrastructure.Services;

/// <summary>
/// Generates a code-map.yaml for a repository using one cheap LLM call.
/// Maps modules, interfaces, implementations, key classes, and dependencies.
/// </summary>
public sealed class CodeMapGenerator(
    string apiKey,
    RetryConfig retryConfig,
    ModelAssignment modelAssignment,
    ILogger<CodeMapGenerator> logger) : ICodeMapGenerator
{
    private const int MaxFileContentChars = 3000;
    private const int MaxTreeDepth = 4;
    private const int MaxFileListing = 200;

    private static readonly HashSet<string> ExcludedDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", "node_modules", "bin", "obj", ".vs", ".idea",
        "__pycache__", ".mypy_cache", ".pytest_cache", "dist", "build",
        ".next", ".nuxt", "coverage", ".terraform"
    };

    public async Task<string> GenerateAsync(
        DetectedProject project,
        string repoPath,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Generating code-map.yaml for {Lang} project at {Path}...",
            project.Language, repoPath);

        var architectureInput = CollectArchitectureInput(project, repoPath);
        var directoryTree = GenerateTree(repoPath, MaxTreeDepth);
        var userPrompt = BuildUserPrompt(project, architectureInput, directoryTree);

        using var client = CreateClient();

        var response = await client.Messages.GetClaudeMessageAsync(
            new MessageParameters
            {
                Model = modelAssignment.Model,
                MaxTokens = modelAssignment.MaxTokens,
                System = new List<SystemMessage> { new(SystemPrompt) },
                Messages = new List<Message>
                {
                    new()
                    {
                        Role = RoleType.User,
                        Content = new List<ContentBase> { new TextContent { Text = userPrompt } }
                    }
                },
                Stream = false
            },
            cancellationToken);

        var yaml = ExtractYaml(response);

        if (!IsValidYaml(yaml))
        {
            logger.LogWarning("Generated code map is not valid YAML, returning empty");
            return string.Empty;
        }

        logger.LogInformation("Generated code-map.yaml ({Chars} chars)", yaml.Length);
        return yaml;
    }

    internal static string CollectArchitectureInput(
        DetectedProject project, string repoPath)
    {
        return project.Language switch
        {
            "C#" => CollectDotNetInput(repoPath),
            "TypeScript" or "JavaScript" => CollectTypeScriptInput(repoPath),
            "Python" => CollectPythonInput(repoPath),
            _ => CollectGenericInput(repoPath)
        };
    }

    internal static string CollectDotNetInput(string repoPath)
    {
        var sections = new List<string>();

        var csprojFiles = Directory.GetFiles(repoPath, "*.csproj", SearchOption.AllDirectories)
            .Where(f => !IsExcludedPath(f))
            .ToList();

        foreach (var csproj in csprojFiles)
        {
            var rel = Path.GetRelativePath(repoPath, csproj);
            var content = TryReadFileTruncated(csproj);
            if (content is not null)
                sections.Add($"### {rel}\n```xml\n{content}\n```");
        }

        var interfaceFiles = Directory.GetFiles(repoPath, "I*.cs", SearchOption.AllDirectories)
            .Where(f => !IsExcludedPath(f))
            .Where(IsInterfaceDirectory)
            .Take(30)
            .ToList();

        foreach (var iFile in interfaceFiles)
        {
            var rel = Path.GetRelativePath(repoPath, iFile);
            var content = TryReadFileTruncated(iFile);
            if (content is not null)
                sections.Add($"### {rel}\n```csharp\n{content}\n```");
        }

        foreach (var entryName in new[] { "Program.cs", "Startup.cs" })
        {
            var entryFiles = Directory.GetFiles(repoPath, entryName, SearchOption.AllDirectories)
                .Where(f => !IsExcludedPath(f));

            foreach (var ef in entryFiles)
            {
                var rel = Path.GetRelativePath(repoPath, ef);
                var content = TryReadFileTruncated(ef);
                if (content is not null)
                    sections.Add($"### {rel}\n```csharp\n{content}\n```");
            }
        }

        var allCs = Directory.GetFiles(repoPath, "*.cs", SearchOption.AllDirectories)
            .Where(f => !IsExcludedPath(f))
            .Select(f => Path.GetRelativePath(repoPath, f))
            .Take(MaxFileListing)
            .ToList();

        sections.Add($"### File listing ({allCs.Count} .cs files)\n" + string.Join('\n', allCs));

        return string.Join("\n\n", sections);
    }

    internal static string CollectTypeScriptInput(string repoPath)
    {
        var sections = new List<string>();

        foreach (var name in new[] { "package.json", "tsconfig.json" })
        {
            var files = Directory.GetFiles(repoPath, name, SearchOption.AllDirectories)
                .Where(f => !IsExcludedPath(f))
                .Take(5);

            foreach (var f in files)
            {
                var rel = Path.GetRelativePath(repoPath, f);
                var content = TryReadFileTruncated(f);
                if (content is not null)
                    sections.Add($"### {rel}\n```json\n{content}\n```");
            }
        }

        var barrels = Directory.GetFiles(repoPath, "index.ts", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(repoPath, "index.js", SearchOption.AllDirectories))
            .Where(f => !IsExcludedPath(f))
            .Take(20);

        foreach (var barrel in barrels)
        {
            var rel = Path.GetRelativePath(repoPath, barrel);
            var content = TryReadFileTruncated(barrel);
            if (content is not null)
                sections.Add($"### {rel}\n```typescript\n{content}\n```");
        }

        var patterns = new[] { "*service*", "*controller*", "*provider*", "*repository*", "*route*", "*router*" };
        var serviceFiles = patterns
            .SelectMany(p => SafeGetFiles(repoPath, p + ".ts")
                .Concat(SafeGetFiles(repoPath, p + ".js")))
            .Where(f => !IsExcludedPath(f))
            .Distinct()
            .Take(20);

        foreach (var sf in serviceFiles)
        {
            var rel = Path.GetRelativePath(repoPath, sf);
            var content = TryReadFileTruncated(sf);
            if (content is not null)
                sections.Add($"### {rel}\n```typescript\n{content}\n```");
        }

        var allTs = SafeGetFiles(repoPath, "*.ts")
            .Concat(SafeGetFiles(repoPath, "*.tsx"))
            .Concat(SafeGetFiles(repoPath, "*.js"))
            .Where(f => !IsExcludedPath(f))
            .Select(f => Path.GetRelativePath(repoPath, f))
            .Take(MaxFileListing)
            .ToList();

        sections.Add($"### File listing ({allTs.Count} TS/JS files)\n" + string.Join('\n', allTs));

        return string.Join("\n\n", sections);
    }

    internal static string CollectPythonInput(string repoPath)
    {
        var sections = new List<string>();

        var initFiles = SafeGetFiles(repoPath, "__init__.py")
            .Where(f => !IsExcludedPath(f))
            .Take(20);

        foreach (var init in initFiles)
        {
            var rel = Path.GetRelativePath(repoPath, init);
            var content = TryReadFileTruncated(init);
            if (content is not null && content.Trim().Length > 0)
                sections.Add($"### {rel}\n```python\n{content}\n```");
        }

        var patterns = new[] { "*interface*", "*abstract*", "*protocol*", "*base*" };
        var structuralFiles = patterns
            .SelectMany(p => SafeGetFiles(repoPath, p + ".py"))
            .Where(f => !IsExcludedPath(f))
            .Distinct()
            .Take(20);

        foreach (var sf in structuralFiles)
        {
            var rel = Path.GetRelativePath(repoPath, sf);
            var content = TryReadFileTruncated(sf);
            if (content is not null)
                sections.Add($"### {rel}\n```python\n{content}\n```");
        }

        foreach (var entryName in new[] { "main.py", "app.py", "manage.py", "wsgi.py" })
        {
            var entryFiles = SafeGetFiles(repoPath, entryName)
                .Where(f => !IsExcludedPath(f));

            foreach (var ef in entryFiles)
            {
                var rel = Path.GetRelativePath(repoPath, ef);
                var content = TryReadFileTruncated(ef);
                if (content is not null)
                    sections.Add($"### {rel}\n```python\n{content}\n```");
            }
        }

        var largestPy = SafeGetFiles(repoPath, "*.py")
            .Where(f => !IsExcludedPath(f))
            .Select(f => new { Path = f, Size = new FileInfo(f).Length })
            .OrderByDescending(f => f.Size)
            .Take(20)
            .ToList();

        foreach (var lf in largestPy)
        {
            var rel = Path.GetRelativePath(repoPath, lf.Path);
            var content = TryReadFileTruncated(lf.Path);
            if (content is not null)
                sections.Add($"### {rel} ({lf.Size} bytes)\n```python\n{content}\n```");
        }

        var allPy = SafeGetFiles(repoPath, "*.py")
            .Where(f => !IsExcludedPath(f))
            .Select(f => Path.GetRelativePath(repoPath, f))
            .Take(MaxFileListing)
            .ToList();

        sections.Add($"### File listing ({allPy.Count} .py files)\n" + string.Join('\n', allPy));

        return string.Join("\n\n", sections);
    }

    internal static string CollectGenericInput(string repoPath)
    {
        return $"### Directory listing\n{GenerateTree(repoPath, MaxTreeDepth)}";
    }

    internal static string BuildUserPrompt(
        DetectedProject project,
        string architectureInput,
        string directoryTree)
    {
        return $"""
            ## Project
            Language: {project.Language}
            Runtime: {project.Runtime ?? "unknown"}
            Frameworks: [{string.Join(", ", project.Frameworks)}]

            ## Architecture-Relevant Files
            {architectureInput}

            ## Directory Structure
            {directoryTree}

            ## Output Format
            Generate a code-map.yaml with this structure:

            ```yaml
            modules:
              - name: <module/project name>
                path: <relative path>
                depends_on: [<other module names>]
                interfaces:
                  - name: <interface name>
                    file: <relative path>
                    does: "<one-line description>"
                    implementations:
                      - name: <class name>
                        file: <relative path>
                key_classes:
                  - name: <class name>
                    file: <relative path>
                    does: "<one-line description>"

            entry_points:
              - file: <relative path>
                type: <main|di_setup|route_config|middleware>
                does: "<one-line description>"

            dependency_graph:
              <module_name>: [<depends_on_module>, ...]
            ```

            Generate the code-map.yaml. Return ONLY valid YAML.
            """;
    }

    internal static string GenerateTree(string rootPath, int maxDepth)
    {
        var lines = new List<string>();
        BuildTreeLines(rootPath, "", maxDepth, 0, lines);
        return string.Join('\n', lines.Take(200));
    }

    internal static bool IsValidYaml(string yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml))
            return false;

        try
        {
            var stream = new YamlStream();
            stream.Load(new StringReader(yaml));
            return stream.Documents.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    internal static string? TryReadFileTruncated(string path)
    {
        try
        {
            var content = File.ReadAllText(path);
            if (content.Length > MaxFileContentChars)
                content = content[..MaxFileContentChars] + "\n... (truncated)";
            return content;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static string ExtractYaml(MessageResponse response)
    {
        var text = response.Content.OfType<TextContent>().FirstOrDefault()?.Text ?? "";
        return StripCodeFences(text);
    }

    internal static string StripCodeFences(string text)
    {
        text = text.Trim();
        if (text.StartsWith("```yaml", StringComparison.OrdinalIgnoreCase))
            text = text[7..];
        else if (text.StartsWith("```"))
            text = text[3..];
        if (text.EndsWith("```"))
            text = text[..^3];

        return text.Trim();
    }

    private static bool IsExcludedPath(string path)
    {
        return ExcludedDirs.Any(dir =>
            path.Contains(Path.DirectorySeparatorChar + dir + Path.DirectorySeparatorChar) ||
            path.Contains(Path.AltDirectorySeparatorChar + dir + Path.AltDirectorySeparatorChar));
    }

    private static bool IsInterfaceDirectory(string path)
    {
        var dir = Path.GetDirectoryName(path) ?? "";
        return dir.Contains("Contracts", StringComparison.OrdinalIgnoreCase) ||
               dir.Contains("Interfaces", StringComparison.OrdinalIgnoreCase) ||
               dir.Contains("Abstractions", StringComparison.OrdinalIgnoreCase) ||
               dir.Contains("Ports", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> SafeGetFiles(string repoPath, string pattern)
    {
        try
        {
            return Directory.GetFiles(repoPath, pattern, SearchOption.AllDirectories);
        }
        catch (IOException)
        {
            return [];
        }
    }

    private static void BuildTreeLines(
        string dirPath, string prefix, int maxDepth, int currentDepth, List<string> lines)
    {
        if (currentDepth >= maxDepth || lines.Count > 200) return;

        try
        {
            var entries = Directory.GetFileSystemEntries(dirPath)
                .Select(e => new { Path = e, Name = Path.GetFileName(e), IsDir = Directory.Exists(e) })
                .Where(e => !ExcludedDirs.Contains(e.Name))
                .OrderBy(e => !e.IsDir)
                .ThenBy(e => e.Name)
                .ToList();

            foreach (var entry in entries)
            {
                var marker = entry.IsDir ? "/" : "";
                lines.Add($"{prefix}{entry.Name}{marker}");

                if (entry.IsDir)
                    BuildTreeLines(entry.Path, prefix + "  ", maxDepth, currentDepth + 1, lines);
            }
        }
        catch (UnauthorizedAccessException) { }
        catch (IOException) { }
    }

    private AnthropicClient CreateClient()
    {
        var factory = new ResilientHttpClientFactory(retryConfig, logger);
        var httpClient = factory.Create();
        return new AnthropicClient(apiKey, httpClient);
    }

    private const string SystemPrompt = """
        You are a code architecture analyst. Generate a code-map in YAML format for this repository.

        Rules:
        - List all modules/projects and their dependencies on each other
        - For each module: list key interfaces and their implementations
        - Identify entry points (main, DI setup, route config)
        - For architecturally significant classes: one-line "does" description
        - Return ONLY valid YAML, no explanation or markdown fences
        - Be precise and concise — this is a reference map, not documentation
        """;
}
