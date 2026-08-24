using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Core.Services.Skills;

/// <summary>
/// p0514: materialises base catalog + operator overlay into ONE root, because one
/// root is the contract every downstream reader already has — the loaders, the
/// concept vocabulary, <c>references/</c>, <c>principles/</c>, <c>patterns/</c>
/// and <c>profiles/</c> all read from <see cref="ISkillsCatalogPath.Root"/>.
/// Copying costs one tree walk; turning the path into a search list would touch
/// every reader instead. Fingerprint-cached the way
/// <see cref="DefaultSourceHandler"/> caches its extract, so an unchanged overlay
/// is not re-copied and an edited one takes effect on the next resolve.
/// </summary>
public sealed class SkillsOverlayMaterializer(ILogger<SkillsOverlayMaterializer> logger)
    : ISkillsOverlayMaterializer
{
    private const string SkillsSubdirectory = "skills";
    private const string MarkerFile = ".overlay";
    private const string RootSuffix = "-overlay";

    public CatalogResolution Apply(CatalogResolution baseResolution, SkillsConfig config)
    {
        ArgumentNullException.ThrowIfNull(baseResolution);
        ArgumentNullException.ThrowIfNull(config);
        if (string.IsNullOrWhiteSpace(config.Overlay)) return baseResolution;

        Validate(config.Overlay);
        var root = ResolveRoot(config);
        var fingerprint = DirectoryFingerprint.Of(config.Overlay);
        var key = $"{baseResolution.Version}|{DirectoryFingerprint.Of(baseResolution.Root)}|{fingerprint}";

        if (IsAlreadyMaterialized(root, key))
            logger.LogInformation(
                "Skill catalog overlay {Fingerprint} already materialized at {Root}", fingerprint, root);
        else
            Materialize(baseResolution.Root, config.Overlay, root, key, fingerprint);

        return baseResolution with { Root = root, Overlay = fingerprint };
    }

    // The same two checks a mounted catalog gets. An overlay that silently
    // resolved to the bare base would ship runs without the operator's own
    // skills and say nothing about it.
    private static void Validate(string overlay)
    {
        if (!Directory.Exists(overlay))
            throw new DirectoryNotFoundException(
                $"skills.overlay directory does not exist: {overlay}");

        if (!Directory.Exists(Path.Combine(overlay, SkillsSubdirectory)))
            throw new DirectoryNotFoundException(
                $"skills.overlay must contain a 'skills/' subdirectory: {overlay}");
    }

    // A sibling of the cache directory, never inside it and never inside the
    // overlay: the union is a THIRD tree, so neither input is ever written to.
    private static string ResolveRoot(SkillsConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.CacheDir))
            throw new InvalidOperationException(
                "skills.cache_dir must be set when skills.overlay is configured — the layered "
                + "catalog is materialized next to it.");

        return Path.TrimEndingDirectorySeparator(config.CacheDir) + RootSuffix;
    }

    private static bool IsAlreadyMaterialized(string root, string key) =>
        Directory.Exists(Path.Combine(root, SkillsSubdirectory))
        && string.Equals(ReadMarker(root), key, StringComparison.Ordinal);

    private void Materialize(string baseRoot, string overlay, string root, string key, string fingerprint)
    {
        // Rebuilt from scratch so a file the previous base or overlay carried and
        // this one does not cannot survive as a ghost in the layered root.
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        DirectoryTreeCopier.CopyOver(baseRoot, root);
        DirectoryTreeCopier.CopyOver(overlay, root);
        WriteMarker(root, key);
        logger.LogInformation(
            "Layered skill catalog overlay {Overlay} ({Fingerprint}) onto base {Base} at {Root}",
            overlay, fingerprint, baseRoot, root);
    }

    private static string? ReadMarker(string root)
    {
        var path = Path.Combine(root, MarkerFile);
        if (!File.Exists(path)) return null;
        try { return File.ReadAllText(path).Trim(); }
        catch (IOException) { return null; }
    }

    private static void WriteMarker(string root, string key)
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, MarkerFile), key);
    }
}
