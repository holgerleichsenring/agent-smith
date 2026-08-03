using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// Loads agentsmith.yml: deserializes into a raw shape, resolves environment
/// variable placeholders in secrets, normalizes per-project pipeline declarations
/// (legacy single-pipeline shim + default-pipeline validation), then materializes
/// catalog references via <see cref="ConfigCatalogResolver"/>.
/// Also fills <see cref="SkillsConfig.CacheDir"/> from
/// <see cref="IAgentSmithPaths.SkillsCatalogRoot"/> when the operator left it blank.
/// </summary>
public sealed class YamlConfigurationLoader(
    RawConfigMaterializer materializer,
    ISystemEventPublisher systemEvents) : IConfigurationLoader
{
    /// <summary>
    /// p0345c: the last successful read this process performed — the dashboard's
    /// drift story compares the file's mtime against this. Set only after the
    /// full load succeeded (a failed parse is not a "read" of effective config).
    /// </summary>
    public ConfigFileReadFact? LastRead { get; private set; }

    public AgentSmithConfig LoadConfig(string configPath)
    {
        var yaml = ReadFile(configPath);
        var raw = Deserialize(yaml, configPath);
        var config = materializer.Materialize(raw);
        RefuseUnresolvable();
        EmitConfigRead(configPath, yaml.Length);
        LastRead = new ConfigFileReadFact(configPath, DateTimeOffset.UtcNow);
        return config;
    }

    /// <summary>
    /// p0391b: this is the ONE-SHOT loader — the CLI, the sandbox agent, the harness. Its
    /// process exists to run one command against one configuration the operator just named,
    /// so an unresolvable reference is refused here and the exit code IS the report. The
    /// server never takes this path: it loads from the DB and stays up to say what is wrong.
    /// The findings are the same findings either way; only what is done with them differs.
    /// </summary>
    private void RefuseUnresolvable()
    {
        var findings = materializer.LastResolutionFindings;
        if (findings.Count == 0) return;
        var joined = string.Join(
            Environment.NewLine + "  - ", findings.Select(f => f.Reason));
        throw new ConfigurationException(
            $"Configuration error(s):{Environment.NewLine}  - {joined}");
    }

    private readonly object _emitLock = new();
    private (string Path, long Mtime, int Size)? _lastEmitted;

    // p0173c: emit ConfigFileReadEvent after a successful agentsmith.yml load (RunId null).
    // p0283b: dedupe — LoadConfig runs on every poll/webhook (~30 sites), so emitting on each
    // call floods the event stream + logs. Publish only when the file actually changed
    // (path + last-write-time + size) since the previous emit.
    private void EmitConfigRead(string path, int sizeBytes)
    {
        try
        {
            var key = (path, System.IO.File.GetLastWriteTimeUtc(path).Ticks, sizeBytes);
            lock (_emitLock)
            {
                if (_lastEmitted == key) return;
                _lastEmitted = key;
            }
            _ = systemEvents.PublishAsync(new ConfigFileReadEvent(
                Source: "config-loader",
                Path: path,
                Kind: ConfigFileKind.AgentSmithYml,
                SizeBytes: sizeBytes,
                RunId: null,
                Timestamp: DateTimeOffset.UtcNow));
        }
        catch
        {
            /* fire-and-warn — never break configuration load on a publish failure */
        }
    }

    private static string ReadFile(string configPath)
    {
        if (!File.Exists(configPath))
            throw new ConfigurationException($"Configuration file not found: {configPath}");

        return File.ReadAllText(configPath);
    }

    private static RawAgentSmithConfig Deserialize(string yaml, string configPath)
    {
        try
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .WithEnumNamingConvention(UnderscoredNamingConvention.Instance)
                .WithTypeConverter(new RawRepoRefYamlConverter())
                .IgnoreUnmatchedProperties()
                .Build();

            return deserializer.Deserialize<RawAgentSmithConfig>(yaml)
                   ?? throw new ConfigurationException("Configuration file is empty.");
        }
        catch (Exception ex) when (ex is not ConfigurationException)
        {
            var detail = ex.InnerException is not null ? $"{ex.Message} -> {ex.InnerException.Message}" : ex.Message;
            throw new ConfigurationException($"Invalid YAML in {configPath}: {detail}");
        }
    }

}
