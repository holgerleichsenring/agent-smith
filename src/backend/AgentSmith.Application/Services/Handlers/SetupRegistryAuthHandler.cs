using System.Xml.Linq;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Registry;
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
/// Relying on the agent to self-heal auth mid-loop is brittle, so this handler
/// pre-stages credentials deterministically before the master ever runs
/// build/restore/test. It runs after CheckoutSource, reads each repo's
/// nuget.config + .npmrc files, matches
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
    GenericRegistryAuthApplier genericApplier,
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

        // p0375: the LLM fallback (invoked only for uncovered ecosystems) needs the
        // run's resolved agent; resolve it lazily so the fast-path-only path — and
        // unit tests without a resolved pipeline — never touch it.
        AgentConfig AgentFactory() => context.Pipeline.Resolved().Agent;

        var totalApplied = 0;
        foreach (var (repoKey, sandbox) in sandboxes)
        {
            totalApplied += await StageInSandboxAsync(repoKey, sandbox, AgentFactory, cancellationToken);
        }

        return CommandResult.Ok(
            $"Registry auth staged: {totalApplied} credential(s) across {sandboxes.Count} sandbox(es).");
    }

    private async Task<int> StageInSandboxAsync(
        string repoKey, ISandbox sandbox, Func<AgentConfig> agentFactory, CancellationToken ct)
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

        // p0375: for any configured registry the deterministic fast-paths did NOT
        // cover, hand the leftover set to the generic path — declared/persisted
        // registry_auth template first, LLM fallback second, token substituted
        // host-side (never sent to the LLM), every gap surfaced loudly.
        var coveredHosts = nugetMatches.Select(m => m.Registry.Host)
            .Concat(npmMatches.Select(m => m.Registry.Host))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        staged += await genericApplier.ApplyAsync(
            repoKey, sandbox, reader, listing, coveredHosts, agentFactory, ct);

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
            if (string.IsNullOrEmpty(content))
            {
                logger.LogWarning("{Repo}: nuget config '{Path}' read back empty — no sources from it.",
                    repoKey, path);
                continue;
            }
            var sources = PackageSourceParser.NuGetSources(content, out var problem);
            if (problem is not null)
                logger.LogWarning("{Repo}: '{Path}' is not readable as XML ({Reason}) — "
                    + "no sources taken from it.", repoKey, path, problem);
            foreach (var (sourceName, sourceUrl) in sources)
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
                matches.Add(new NugetMatch(sourceName, sourceUrl, reg));
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
            foreach (var (registryKey, registryUrl) in PackageSourceParser.NpmRegistries(content))
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
                matches.Add(new NpmMatch(registryKey, registryUrl, reg));
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
        // p0374: dedup by the (mapping-key, url) PAIR, not the url alone — two scopes
        // may point at the same feed and each needs its own `@scope:registry=` line
        // emitted globally.
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<NpmMatch>();
        foreach (var m in matches)
            if (seen.Add(m.RegistryKey + "" + m.RegistryUrl)) result.Add(m);
        return result;
    }

    private static string BuildNuGetUserConfig(IReadOnlyList<NugetMatch> matches)
    {
        // p0374: define the SOURCES globally too, not only credentials. Source
        // definitions otherwise live only in the repo's own nuget.config, so a probe
        // OUTSIDE the repo tree — e.g. a /tmp scratch project the coding agent spins
        // up to verify a package exists — saw just this global config: credentials
        // with NO sources → "no sources found" → NU1100 → the agent wrongly concludes
        // the private package is unavailable and skips the work (live: run …5dc6
        // abandoned the Wolverine migration though 1.1.17 was on the feed). Emitting
        // the authenticated sources here makes the feed resolvable from anywhere;
        // nuget.org is included so public probes work too. Repo-config merges dedupe
        // by source key, so the real build is unchanged.
        var sources = new XElement("packageSources",
            new XElement("add", new XAttribute("key", "nuget.org"),
                new XAttribute("value", "https://api.nuget.org/v3/index.json")));
        sources.Add(matches.Select(m => new XElement("add",
            new XAttribute("key", m.SourceName), new XAttribute("value", m.SourceUrl))));

        var creds = new XElement("packageSourceCredentials",
            matches.Select(m => new XElement(SanitizeXmlName(m.SourceName),
                new XElement("add", new XAttribute("key", "Username"),
                    new XAttribute("value", string.IsNullOrEmpty(m.Registry.Username) ? "any" : m.Registry.Username)),
                new XElement("add", new XAttribute("key", "ClearTextPassword"),
                    new XAttribute("value", m.Registry.Token)))));
        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("configuration", sources, creds));
        return doc.Declaration + "\n" + doc.ToString();
    }

    private static string BuildNpmrc(IReadOnlyList<NpmMatch> matches)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("always-auth=true");
        foreach (var m in matches)
        {
            // p0374: emit the registry MAPPING globally too (`registry=` /
            // `@scope:registry=`), not just the auth token. The mapping otherwise
            // lives only in the repo's .npmrc, so an `npm view`/install the coding
            // agent runs OUTSIDE the repo tree routes a scoped package to the public
            // registry → 404 → wrongly "package unavailable" (the npm twin of the
            // NuGet /tmp-probe bug). With the mapping here, the private feed resolves
            // from anywhere; the auth token below is keyed by host and already global.
            sb.AppendLine($"{m.RegistryKey}={m.RegistryUrl}");
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

    private sealed record NugetMatch(string SourceName, string SourceUrl, RegistryConfig Registry);
    private sealed record NpmMatch(string RegistryKey, string RegistryUrl, RegistryConfig Registry);
}
