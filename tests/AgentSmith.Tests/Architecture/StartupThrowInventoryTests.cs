using FluentAssertions;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// p0391b — THE INVENTORY, as a mechanism rather than a list in a document.
///
/// p0391a ruled that aborting startup is never justifiable and converted the paths it
/// found. p0393 then found another one in a path p0391a had already written a probe for.
/// A ruling is not a mechanism: what stops the next one is that every throw on a
/// composition or configuration path has to be ACCOUNTED FOR here, either by being
/// converted to a finding or by carrying a reason it stays fatal.
///
/// Adding a throw to one of the scanned files fails this test until its author writes down
/// which of the two it is.
/// </summary>
public sealed class StartupThrowInventoryTests
{
    // Composition, hosting and configuration. Everything here runs before or during host
    // start, or produces the configuration that does — the places where a throw is a dead
    // process rather than a failed request.
    private static readonly string[] ScannedDirectories =
    [
        "AgentSmith.Server/Services/Startup",
        "AgentSmith.Server/Services/Hosting",
        "AgentSmith.Server/Extensions",
        "AgentSmith.Server/Services",
        "AgentSmith.Infrastructure.Core/Services/Configuration",
    ];

    /// <summary>
    /// file name -> why a throw in it does not kill the server. Each entry is a claim that
    /// has to stay true; the accompanying assertions in this suite and in
    /// StartupResilienceTests are what keep it honest.
    /// </summary>
    private static readonly Dictionary<string, string> Accounted = new()
    {
        ["UnavailableJobSpawner.cs"] =
            "Refuses the WORK, not the process: a run dispatched with no sandbox backend fails "
            + "with the reason. p0391a's rule permits exactly this.",
        ["DockerJobSpawner.cs"] =
            "Per-run spawn failure at request time, not composition.",
        ["ConnectionRepoUrlBuilder.cs"] =
            "Caught by ConfigCatalogResolver.TryBuildProject — becomes that project's finding.",
        ["RepoGlobExpander.cs"] =
            "Caught by ConfigCatalogResolver.TryBuildProject — becomes that project's finding.",
        ["EffectiveTriggerBuilder.cs"] =
            "Caught by RawConfigMaterializer.ApplyEffectiveTriggers — becomes that project's finding.",
        ["RawRepoRefYamlConverter.cs"] =
            "YAML binding; both loaders catch it (the file loader as a parse error, the DB "
            + "loader as a configuration finding).",
        ["SecretsProvider.cs"] =
            "Not on a startup path: the materializer resolves env references with a null-coalesce, "
            + "so a missing secret is a provider-construction failure at request time.",
        ["YamlConfigurationLoader.cs"] =
            "STAYS FATAL, deliberately. This is the one-shot loader — CLI, sandbox agent, harness. "
            + "That process exists to run one command against one file the operator just named, so "
            + "the exit code IS the report and there is nothing to keep alive. The server never "
            + "takes this path; it loads from the DB via DbConfigurationLoader, which never throws. "
            + "Operator action: `agentsmith config validate` prints the same findings.",
    };

    [Fact]
    public void StartupPaths_NoRemainingThrow_IsUnaccountedFor()
    {
        var unaccounted = ScanThrows()
            .Where(hit => !Accounted.ContainsKey(hit.File))
            .Select(hit => $"{hit.File}:{hit.Line} — {hit.Text}")
            .ToList();

        unaccounted.Should().BeEmpty(
            "every throw on a composition or configuration path must either become a "
            + "StartupFinding (p0391a's ruling: a dead process reports nothing) or be listed "
            + "in StartupThrowInventoryTests.Accounted with the reason it stays fatal and what "
            + "an operator can do about it. Unaccounted: "
            + string.Join(" | ", unaccounted));
    }

    [Fact]
    public void StartupPaths_EveryAccountedEntry_StillExists()
    {
        // An allowlist that outlives its throw is a lie about what the code does — the next
        // author reads it as permission for a throw nobody reviewed.
        var scannedFiles = ScanThrows().Select(hit => hit.File).ToHashSet();

        Accounted.Keys.Where(file => !scannedFiles.Contains(file)).Should().BeEmpty(
            "an accounted throw that no longer exists must be removed from the inventory");
    }

    private static IEnumerable<(string File, int Line, string Text)> ScanThrows()
    {
        var root = ResolveSrcBackendRoot();
        foreach (var relative in ScannedDirectories)
        {
            var directory = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(directory)) continue;

            // Top level only: a nested folder is its own concern and is listed explicitly
            // when it belongs to composition.
            foreach (var file in Directory.EnumerateFiles(directory, "*.cs", SearchOption.TopDirectoryOnly))
                foreach (var hit in ThrowsIn(file))
                    yield return hit;
        }
    }

    private static IEnumerable<(string File, int Line, string Text)> ThrowsIn(string path)
    {
        var name = Path.GetFileName(path);
        var lines = File.ReadAllLines(path);
        for (var i = 0; i < lines.Length; i++)
        {
            var text = lines[i].Trim();
            if (text.StartsWith("//") || text.StartsWith("///")) continue;
            if (!text.Contains("throw new")
                && !text.Contains("Environment.Exit(")
                && !text.Contains("Environment.FailFast(")) continue;
            yield return (name, i + 1, text);
        }
    }

    private static string ResolveSrcBackendRoot()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 10; i++)
        {
            var candidate = Path.Combine(dir, "src", "backend");
            if (Directory.Exists(candidate)) return candidate;
            var parent = Directory.GetParent(dir);
            if (parent is null) break;
            dir = parent.FullName;
        }
        throw new InvalidOperationException(
            $"Could not locate src/backend from test base directory '{AppContext.BaseDirectory}'");
    }
}
