using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AgentSmith.Infrastructure.Services;

/// <summary>
/// YamlDotNet-backed parser for context.yaml summaries (p0161). The summary
/// surface is meta.workdir + stack.lang — the two fields the orchestrator
/// consumes before any real work. Rest of the schema (arch, quality, state)
/// passes through silently via IgnoreUnmatchedProperties.
/// </summary>
public sealed class ContextYamlParser : IContextYamlParser
{
    public ContextYamlSummary? TryParse(string yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml)) return null;
        Shape? doc;
        try
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();
            doc = deserializer.Deserialize<Shape>(yaml);
        }
        catch (Exception ex) when (ex is YamlDotNet.Core.YamlException or InvalidCastException)
        {
            return null;
        }

        if (doc?.Meta is null) return null;
        if (string.IsNullOrWhiteSpace(doc.Meta.Workdir))
            throw new InvalidOperationException(
                "context.yaml missing required field meta.workdir (p0161). "
                + "Single-stack: set workdir: \".\". Monorepo sub-stack: set the relative sub-tree path.");

        return new ContextYamlSummary(doc.Meta.Workdir.Trim(), doc.Stack?.Lang?.Trim());
    }

    private sealed class Shape
    {
        public MetaBlock? Meta { get; set; }
        public StackBlock? Stack { get; set; }
    }

    private sealed class MetaBlock
    {
        public string? Workdir { get; set; }
    }

    private sealed class StackBlock
    {
        public string? Lang { get; set; }
    }
}
