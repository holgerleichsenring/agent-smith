using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AgentSmith.Infrastructure.Core.Services.Skills;

/// <summary>
/// p0504: reads the domain profiles the RESOLVED skills catalog carries, from
/// <c>&lt;root&gt;/profiles/&lt;name&gt;/profile.yaml</c>. Which profiles exist is a
/// property of the pin a run resolved, which is why a miss is a refusal rather
/// than a degradation — a domain the catalog does not carry is indistinguishable
/// from a typo.
/// </summary>
public sealed class FileDomainProfileCatalog(
    ISkillsCatalogPath catalogPath,
    ILogger<FileDomainProfileCatalog> logger) : IDomainProfileCatalog
{
    private const string ProfilesDirectory = "profiles";

    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public string Origin => catalogPath.Origin;

    public IReadOnlyList<string> KnownDomains
    {
        get
        {
            var root = TryRoot();
            if (root is null) return [];
            var directory = Path.Combine(root, ProfilesDirectory);
            if (!Directory.Exists(directory)) return [];
            return [.. Directory.EnumerateDirectories(directory)
                .Select(Path.GetFileName)
                .Where(n => !string.IsNullOrEmpty(n))
                .Select(n => n!)
                .OrderBy(n => n, StringComparer.Ordinal)];
        }
    }

    public DomainProfile? Find(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain)) return null;
        var root = TryRoot();
        if (root is null) return null;
        var path = Path.Combine(root, ProfilesDirectory, domain.Trim(), "profile.yaml");
        if (!File.Exists(path)) return null;

        DomainProfileYaml? parsed;
        try { parsed = Deserializer.Deserialize<DomainProfileYaml>(File.ReadAllText(path)); }
        catch (Exception ex) when (ex is YamlDotNet.Core.YamlException or IOException)
        {
            logger.LogWarning(ex, "Domain profile '{Path}' could not be read — treating it as absent.", path);
            return null;
        }

        if (parsed is null || string.IsNullOrWhiteSpace(parsed.Image))
        {
            logger.LogWarning(
                "Domain profile '{Path}' declares no image — treating it as absent.", path);
            return null;
        }

        return new DomainProfile(
            domain.Trim(),
            parsed.Image.Trim(),
            [.. (parsed.CompatibleImages ?? []).Where(i => !string.IsNullOrWhiteSpace(i)).Select(i => i.Trim())],
            [.. (parsed.Verify ?? [])
                .Where(v => !string.IsNullOrWhiteSpace(v.Stage) && !string.IsNullOrWhiteSpace(v.Command))
                .Select(v => new DomainProfileCommand(
                    v.Stage!.Trim(), v.Command!.Trim(), Condition(v.WhenPresent)))]);
    }

    // p0513: a blank when_present is no condition at all, not a path that never
    // exists — a profile author writing `when_present: ""` must not silently lose
    // the command.
    private static string? Condition(string? whenPresent) =>
        string.IsNullOrWhiteSpace(whenPresent) ? null : whenPresent.Trim();

    // The catalog path throws until the bootstrap service has resolved it; a doctor
    // check or a unit-scoped run must not crash on that.
    private string? TryRoot()
    {
        try { return catalogPath.Root; }
        catch (InvalidOperationException) { return null; }
    }
}
