using System.Xml.Linq;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0198: pre-stage private-feed credentials inside every sandbox.
///
/// The architectural fix for the p0191 design flaw: p0191 assumed the master
/// agent encounters NU1301 / EAUTH and calls <c>get_artifact_credentials</c>.
/// In practice the agent never sees those errors — TestHandler runs
/// <c>dotnet test</c> directly without an LLM. This handler runs deterministically
/// after CheckoutSource, reads each repo's nuget.config + .npmrc files, matches
/// declared source URLs against the operator's <c>registries:</c> block, and
/// writes user-level credential files (~/.nuget/NuGet/NuGet.Config and
/// ~/.npmrc) inside each sandbox so every downstream step inherits working auth.
///
/// Every decision is logged at info level with file path + host + outcome —
/// operator must never have to wonder "why is it still failing".
/// </summary>
public sealed class SetupRegistryAuthHandler(
    ISandboxFileReaderFactory readerFactory,
    AgentSmithConfig config,
    ILogger<SetupRegistryAuthHandler> logger)
    : ICommandHandler<SetupRegistryAuthContext>
{
    private const string WorkRoot = "/work";
    private const string UserNuGetConfigPath = "/root/.nuget/NuGet/NuGet.Config";
    private const string UserNpmrcPath = "/root/.npmrc";

    public async Task<CommandResult> ExecuteAsync(
        SetupRegistryAuthContext context, CancellationToken cancellationToken)
    {
        // Three legitimate "nothing to do" cases — ALL return Ok cleanly so a
        // docs-only repo / public-only project / passive pipeline doesn't
        // block. Downstream build/test steps will still fail loudly with
        // NU1301 / EAUTH if private auth is actually needed.
        if (config.Registries.Count == 0)
        {
            logger.LogInformation(
                "No `registries:` block in agentsmith.yml — skipping cleanly. Projects without private feeds (docs-only, public-only) need no setup.");
            return CommandResult.Ok("No registries configured; no credentials staged.");
        }

        logger.LogInformation(
            "Configured registries: [{Hosts}] | tokens resolved: {Resolved}/{Total} | missing: [{Missing}]",
            string.Join(", ", config.Registries.Select(r => r.Host)),
            config.Registries.Count(r => !string.IsNullOrEmpty(r.Token)),
            config.Registries.Count,
            string.Join(", ", config.Registries.Where(r => string.IsNullOrEmpty(r.Token)).Select(r => r.Host)));

        if (!context.Pipeline.TryGet<IReadOnlyDictionary<string, ISandbox>>(
                ContextKeys.Sandboxes, out var sandboxes) || sandboxes is null || sandboxes.Count == 0)
        {
            logger.LogInformation(
                "No sandboxes available — nothing to stage. Skipping cleanly (downstream steps will fail loudly if they actually need auth).");
            return CommandResult.Ok("No sandboxes; no credentials staged.");
        }

        var totalApplied = 0;
        foreach (var (repoKey, sandbox) in sandboxes)
        {
            totalApplied += await StageInSandboxAsync(repoKey, sandbox, cancellationToken);
        }

        return CommandResult.Ok(
            $"Registry auth staged: {totalApplied} credential(s) across {sandboxes.Count} sandbox(es).");
    }

    private async Task<int> StageInSandboxAsync(string repoKey, ISandbox sandbox, CancellationToken ct)
    {
        var reader = readerFactory.Create(sandbox);
        var listing = await reader.ListAsync(WorkRoot, maxDepth: 6, ct);

        var nugetMatches = await CollectNuGetMatchesAsync(listing, reader, repoKey, ct);
        var npmMatches = await CollectNpmMatchesAsync(listing, reader, repoKey, ct);

        var staged = 0;
        if (nugetMatches.Count > 0)
        {
            await reader.WriteAsync(UserNuGetConfigPath, BuildNuGetUserConfig(nugetMatches), ct);
            logger.LogInformation(
                "{Repo}: staged {Count} NuGet credential(s) at {Path}: [{Sources}]",
                repoKey, nugetMatches.Count, UserNuGetConfigPath,
                string.Join(", ", nugetMatches.Select(m => m.SourceName)));
            staged += nugetMatches.Count;
        }
        else
        {
            logger.LogInformation("{Repo}: no NuGet credential matches.", repoKey);
        }

        if (npmMatches.Count > 0)
        {
            await reader.WriteAsync(UserNpmrcPath, BuildNpmrc(npmMatches), ct);
            logger.LogInformation(
                "{Repo}: staged {Count} npm credential(s) at {Path}.",
                repoKey, npmMatches.Count, UserNpmrcPath);
            staged += npmMatches.Count;
        }
        else
        {
            logger.LogInformation("{Repo}: no npm credential matches.", repoKey);
        }

        return staged;
    }

    private async Task<IReadOnlyList<NugetMatch>> CollectNuGetMatchesAsync(
        IReadOnlyList<string> listing, ISandboxFileReader reader, string repoKey, CancellationToken ct)
    {
        var configs = listing
            .Where(p => p.EndsWith("/nuget.config", StringComparison.OrdinalIgnoreCase)
                     || p.EndsWith("/NuGet.config", StringComparison.OrdinalIgnoreCase)
                     || p.EndsWith("/NuGet.Config", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (configs.Count == 0)
        {
            logger.LogInformation("{Repo}: no nuget.config files found under /work.", repoKey);
            return Array.Empty<NugetMatch>();
        }

        var matches = new List<NugetMatch>();
        foreach (var path in configs)
        {
            var content = await reader.TryReadAsync(path, ct);
            if (string.IsNullOrEmpty(content)) continue;
            foreach (var (sourceName, sourceUrl) in TryParseNuGetSources(content, path, repoKey))
            {
                var reg = FindMatchingRegistry(sourceUrl);
                if (reg is null)
                {
                    logger.LogInformation(
                        "{Repo}: nuget source '{Source}' ({Url}) — no matching registry (public source or operator hasn't configured this host).",
                        repoKey, sourceName, sourceUrl);
                    continue;
                }
                logger.LogInformation(
                    "{Repo}: nuget source '{Source}' ({Host}) → matched registry '{RegHost}'.",
                    repoKey, sourceName, new Uri(sourceUrl).Host, reg.Host);
                matches.Add(new NugetMatch(sourceName, reg));
            }
        }
        return DedupBySource(matches);
    }

    private async Task<IReadOnlyList<NpmMatch>> CollectNpmMatchesAsync(
        IReadOnlyList<string> listing, ISandboxFileReader reader, string repoKey, CancellationToken ct)
    {
        var rcFiles = listing
            .Where(p => p.EndsWith("/.npmrc", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (rcFiles.Count == 0)
        {
            logger.LogInformation("{Repo}: no .npmrc files found under /work.", repoKey);
            return Array.Empty<NpmMatch>();
        }

        var matches = new List<NpmMatch>();
        foreach (var path in rcFiles)
        {
            var content = await reader.TryReadAsync(path, ct);
            if (string.IsNullOrEmpty(content)) continue;
            foreach (var registryUrl in TryParseNpmRegistries(content))
            {
                var reg = FindMatchingRegistry(registryUrl);
                if (reg is null)
                {
                    logger.LogInformation(
                        "{Repo}: npm registry {Url} — no matching registry (public registry or operator hasn't configured this host).",
                        repoKey, registryUrl);
                    continue;
                }
                logger.LogInformation(
                    "{Repo}: npm registry {Url} → matched registry '{RegHost}'.", repoKey, registryUrl, reg.Host);
                matches.Add(new NpmMatch(registryUrl, reg));
            }
        }
        return DedupByUrl(matches);
    }

    private RegistryConfig? FindMatchingRegistry(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return null;
        var host = uri.Host;
        foreach (var reg in config.Registries)
        {
            if (string.Equals(host, reg.Host, StringComparison.OrdinalIgnoreCase)) return reg;
            if (host.EndsWith("." + reg.Host, StringComparison.OrdinalIgnoreCase)) return reg;
        }
        return null;
    }

    private static IEnumerable<(string Name, string Url)> TryParseNuGetSources(
        string content, string path, string repoKey)
    {
        XDocument doc;
        try { doc = XDocument.Parse(content); }
        catch { yield break; }
        var ns = doc.Root?.Name.Namespace ?? XNamespace.None;
        var sources = doc.Descendants(ns + "packageSources")
            .Elements(ns + "add")
            .Where(e => !string.Equals(
                (string?)e.Attribute("key"), "clear", StringComparison.OrdinalIgnoreCase));
        foreach (var e in sources)
        {
            var name = (string?)e.Attribute("key");
            var url = (string?)e.Attribute("value");
            if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(url))
                yield return (name, url);
        }
    }

    private static IEnumerable<string> TryParseNpmRegistries(string content)
    {
        foreach (var rawLine in content.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith(';')) continue;
            var idx = line.IndexOf('=');
            if (idx <= 0) continue;
            var key = line[..idx].Trim();
            var value = line[(idx + 1)..].Trim();
            // Match `registry=...` and `@scope:registry=...` lines.
            if (string.Equals(key, "registry", StringComparison.OrdinalIgnoreCase)
                || key.EndsWith(":registry", StringComparison.OrdinalIgnoreCase))
            {
                yield return value;
            }
        }
    }

    private static IReadOnlyList<NugetMatch> DedupBySource(IEnumerable<NugetMatch> matches)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<NugetMatch>();
        foreach (var m in matches)
            if (seen.Add(m.SourceName)) result.Add(m);
        return result;
    }

    private static IReadOnlyList<NpmMatch> DedupByUrl(IEnumerable<NpmMatch> matches)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<NpmMatch>();
        foreach (var m in matches)
            if (seen.Add(m.RegistryUrl)) result.Add(m);
        return result;
    }

    private static string BuildNuGetUserConfig(IReadOnlyList<NugetMatch> matches)
    {
        var creds = new XElement("packageSourceCredentials",
            matches.Select(m => new XElement(SanitizeXmlName(m.SourceName),
                new XElement("add", new XAttribute("key", "Username"),
                    new XAttribute("value", string.IsNullOrEmpty(m.Registry.Username) ? "any" : m.Registry.Username)),
                new XElement("add", new XAttribute("key", "ClearTextPassword"),
                    new XAttribute("value", m.Registry.Token)))));
        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("configuration", creds));
        return doc.Declaration + "\n" + doc.ToString();
    }

    private static string BuildNpmrc(IReadOnlyList<NpmMatch> matches)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("always-auth=true");
        foreach (var m in matches)
        {
            // Strip scheme so `//host/path/:_authToken=...` keys correctly.
            var noScheme = m.RegistryUrl.Substring(m.RegistryUrl.IndexOf("//", StringComparison.Ordinal));
            if (!noScheme.EndsWith('/')) noScheme += '/';
            sb.AppendLine($"{noScheme}:_authToken={m.Registry.Token}");
        }
        return sb.ToString();
    }

    // NuGet.Config XML element names must be valid XML identifiers; source
    // names can contain dots / underscores which are fine. Replace anything
    // else (shouldn't happen in practice) with underscore so the file parses.
    private static string SanitizeXmlName(string name)
    {
        var chars = name.Select(c => char.IsLetterOrDigit(c) || c == '_' || c == '.' || c == '-' ? c : '_');
        var sanitized = new string(chars.ToArray());
        return char.IsDigit(sanitized[0]) ? "_" + sanitized : sanitized;
    }

    private sealed record NugetMatch(string SourceName, RegistryConfig Registry);
    private sealed record NpmMatch(string RegistryUrl, RegistryConfig Registry);
}
