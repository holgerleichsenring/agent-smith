using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Services.Security;

namespace AgentSmith.Infrastructure.Services.Pocs;

/// <summary>
/// 2026-11-23-933dc: every catalog pattern a PoC is scanned with, loaded and compiled as the security scan does — but
/// strict for the 'secrets' catalog: none loaded, or one the compiler dropped as invalid, is a failure naming the
/// directory or the pattern id, never a clean pass.
/// </summary>
public sealed class PocPatternSet(
    PatternsDirectoryResolver directories, PatternDefinitionLoader loader, PatternCompiler compiler)
{
    public const string SecretsCategory = "secrets";

    public (IReadOnlyList<CompiledPattern> Patterns, string? Failure) Load()
    {
        var directory = directories.Resolve();
        var definitions = loader.LoadFromDirectory(directory);
        var secrets = definitions.Where(IsSecret).ToList();
        if (secrets.Count == 0) return ([], $"no secret patterns loaded from {directory}; the PoC cannot be scanned for secrets");
        var compiled = compiler.Compile(definitions);
        var compiledIds = compiled.Select(c => c.Definition.Id).ToHashSet(StringComparer.Ordinal);
        if (secrets.FirstOrDefault(s => !compiledIds.Contains(s.Id)) is { } dropped)
            return ([], $"the secret pattern '{dropped.Id}' in {directory} did not compile; a secret it would find could not be found");
        return (compiled, null);
    }

    public static bool IsSecret(PatternDefinition definition) =>
        string.Equals(definition.Category, SecretsCategory, StringComparison.OrdinalIgnoreCase);
}
