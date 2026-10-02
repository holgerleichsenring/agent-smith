using System.Text.Json.Nodes;
using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration.Retired;
using AgentSmith.Tests.Architecture;
using FluentAssertions;

namespace AgentSmith.Tests.Configuration;

/// <summary>
/// The loader ignores keys it does not know, on purpose — an operator's server must keep
/// starting over a key that stopped mattering. That makes the schema the only typo guard an
/// operator has, and these rules keep it telling the truth in both directions: every key the
/// loader binds is declared, nothing is declared that the loader ignores, and every object
/// closes its properties so a misspelt key is reported rather than accepted.
/// </summary>
public sealed class ConfigSchemaCoverageTests
{
    /// <summary>Bound values the schema refuses on purpose, each with the reason.</summary>
    private static readonly IReadOnlyDictionary<string, string> NarrowedOnPurpose =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["connections.*.type=local"] =
                "a connection is a git host repos are discovered under; a local path is a repos: entry",
        };

    /// <summary>Property tables that are mixed into others, which close them.</summary>
    private static readonly IReadOnlyDictionary<string, string> Mixins =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["#/$defs/triggerFields"] =
                "the shared trigger block; webhookTrigger and jiraTrigger each close it with unevaluatedProperties",
        };

    private static ConfigSchemaCoverage Loader() =>
        ConfigSchemaCoverage.Of(typeof(RawAgentSmithConfig), ConfigSchemaFile.Root);

    /// <summary>tool_runner is read from the file by its own bootstrap reader, not by the loader.</summary>
    private static ConfigSchemaCoverage ToolRunner() =>
        ConfigSchemaCoverage.Of(typeof(ToolRunnerConfig),
            ConfigSchemaFile.Properties(ConfigSchemaFile.Root)["tool_runner"], "tool_runner");

    [Fact]
    public void ConfigSchema_DeclaresEveryKeyTheLoaderBinds() =>
        Loader().Undeclared.Concat(ToolRunner().Undeclared)
            .Where(key => !NarrowedOnPurpose.ContainsKey(key))
            .Should().BeEmpty(
            "a key the loader reads but the schema rejects fails every editor that validates the file");

    [Fact]
    public void ConfigSchema_DeclaresNothingTheLoaderIgnores() =>
        Loader().Unbound.Where(key => key != "tool_runner" && !KeptDeprecated(key))
            .Concat(ToolRunner().Unbound)
            .Should().BeEmpty("a declared key nothing reads is accepted and silently does nothing");

    // 2026-10-02-5ab2f: a retired root key may stay declared so an editor validating an older
    // file does not flag it — only as a RetiredConfigKeys row the schema marks deprecated.
    private static bool KeptDeprecated(string key) =>
        RetiredConfigKeys.All.Any(r => r.Path == key)
        && ConfigSchemaFile.Properties(ConfigSchemaFile.Root).TryGetValue(key, out var node)
        && node["deprecated"] is JsonValue deprecated && deprecated.GetValue<bool>();

    [Fact]
    public void ConfigSchema_PipelineStorage_IsDeclaredDeprecated() =>
        KeptDeprecated("pipeline_storage").Should().BeTrue(
            "the operator keeps the retired block declared so older files validate");

    [Fact]
    public void ConfigSchema_EveryObjectClosesItsProperties() =>
        OpenObjects(ConfigSchemaFile.Root, "#").Where(path => !Mixins.ContainsKey(path)).Should().BeEmpty(
            "an object that accepts any key lets a misspelt one through unreported");

    private static IEnumerable<string> OpenObjects(JsonNode? node, string path)
    {
        if (node is JsonArray array)
            return array.SelectMany((item, i) => OpenObjects(item, $"{path}/{i}"));
        if (node is not JsonObject obj) return [];
        var own = obj.ContainsKey("properties") && !IsClosed(obj) ? [path] : Array.Empty<string>();
        return own.Concat(obj.SelectMany(pair => OpenObjects(pair.Value, $"{path}/{pair.Key}")));
    }

    private static bool IsClosed(JsonObject obj) =>
        obj["additionalProperties"] is JsonValue a && !a.GetValue<bool>()
        || obj["unevaluatedProperties"] is JsonValue u && !u.GetValue<bool>();
}
