using AgentSmith.Application.Models.Registry;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Registry;

/// <summary>
/// p0375 decide-once persistence: reads and writes the <c>registry_auth</c>
/// section of the repo's context.yaml (in the sandbox checkout) via the typed
/// codec. A present section — operator-authored or persisted from a prior run —
/// is authoritative: the LLM stager is skipped and the template replayed.
/// Persisted content carries placeholders only; a secret never lands in the repo.
/// </summary>
public sealed class RegistryAuthTemplateStore(
    IContextYamlRegistryAuthCodec codec,
    ILogger<RegistryAuthTemplateStore> logger)
{
    public async Task<IReadOnlyList<StagedAuthFile>?> TryReadAsync(
        string repoKey, IReadOnlyList<string> listing, ISandboxFileReader reader, CancellationToken ct)
    {
        foreach (var path in ContextYamlPaths(listing))
        {
            var yaml = await reader.TryReadAsync(path, ct);
            if (string.IsNullOrWhiteSpace(yaml)) continue;
            var section = codec.Read(yaml);
            if (section is null) continue;
            logger.LogInformation(
                "{Repo}: registry_auth section found in {Path} ({Count} file template(s)) — replaying LLM-free.",
                repoKey, path, section.Files.Count);
            return section.Files.Select(f => new StagedAuthFile(f.Path, f.Content)).ToList();
        }
        return null;
    }

    public async Task PersistAsync(
        string repoKey, IReadOnlyList<string> listing, ISandboxFileReader reader,
        IReadOnlyList<StagedAuthFile> templatedFiles, CancellationToken ct)
    {
        var path = ContextYamlPaths(listing).FirstOrDefault();
        if (path is null)
        {
            logger.LogInformation(
                "{Repo}: no context.yaml in the checkout — registry_auth template not persisted "
                + "(the stager will run again next run).", repoKey);
            return;
        }

        var yaml = await reader.TryReadAsync(path, ct) ?? string.Empty;
        var section = new ContextYamlRegistryAuth(
            templatedFiles.Select(f => new ContextYamlRegistryAuthFile(f.Path, f.Content)).ToList());
        await reader.WriteAsync(path, codec.Upsert(yaml, section), ct);
        logger.LogInformation(
            "{Repo}: persisted registry_auth template ({Count} file(s), placeholders only) into {Path}.",
            repoKey, templatedFiles.Count, path);
    }

    private static IEnumerable<string> ContextYamlPaths(IReadOnlyList<string> listing) =>
        listing
            .Where(p => p.Contains("/.agentsmith/contexts/", StringComparison.Ordinal)
                     && p.EndsWith("/context.yaml", StringComparison.Ordinal))
            .OrderBy(p => p, StringComparer.Ordinal);
}
