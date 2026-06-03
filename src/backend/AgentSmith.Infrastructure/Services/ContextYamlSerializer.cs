using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AgentSmith.Infrastructure.Services;

/// <summary>
/// p0193: one YamlDotNet builder configuration shared by emit + consume.
/// Parse-failures from agent-written context.yaml become unrepresentable —
/// the writer is the same code as the reader, applied via Serialize().
/// </summary>
public sealed class ContextYamlSerializer : IContextYamlSerializer
{
    private static readonly ISerializer YamlSerializer = new SerializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        .Build();

    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public string Serialize(ContextYamlDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (string.IsNullOrWhiteSpace(document.Meta?.Workdir))
            throw new InvalidOperationException(
                "ContextYamlDocument.Meta.Workdir is required (p0161).");
        return YamlSerializer.Serialize(document);
    }

    public ContextYamlParseResult Parse(string yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml)) return ContextYamlParseResult.Empty();
        ReadShape? doc;
        try
        {
            doc = YamlDeserializer.Deserialize<ReadShape>(yaml);
        }
        catch (YamlException ex)
        {
            return ContextYamlParseResult.Error(FormatYamlError(ex, yaml));
        }
        catch (InvalidCastException ex)
        {
            return ContextYamlParseResult.Error("type mismatch: " + ex.Message);
        }

        if (doc?.Meta is null) return ContextYamlParseResult.Empty();
        if (string.IsNullOrWhiteSpace(doc.Meta.Workdir))
            throw new InvalidOperationException(
                "context.yaml missing required field meta.workdir (p0161). "
                + "Single-stack: set workdir: \".\". Monorepo sub-stack: set the relative sub-tree path.");

        return ContextYamlParseResult.Ok(
            new ContextYamlSummary(
                doc.Meta.Workdir.Trim(),
                doc.Stack?.Lang?.Trim(),
                doc.Prerequisites?.Trim()));
    }

    private static string FormatYamlError(YamlException ex, string yaml)
    {
        var line = ex.Start.Line;
        var col = ex.Start.Column;
        var hint = BuildHint(yaml, (int)line, (int)col);
        var prefix = line > 0 ? $"(Line: {line}, Col: {col}) " : string.Empty;
        return prefix + ex.Message + hint;
    }

    private static string BuildHint(string yaml, int line, int col)
    {
        if (line <= 0) return string.Empty;
        var sourceLine = TryGetLine(yaml, line);
        if (sourceLine is null) return string.Empty;
        if (col >= 1 && col <= sourceLine.Length && sourceLine[col - 1] == '@')
            return " (hint: quote npm scoped packages, e.g. \"@scope/pkg\")";
        return string.Empty;
    }

    private static string? TryGetLine(string yaml, int line)
    {
        var lines = yaml.Split('\n');
        return line >= 1 && line <= lines.Length ? lines[line - 1].TrimEnd('\r') : null;
    }

    private sealed class ReadShape
    {
        public MetaBlock? Meta { get; set; }
        public StackBlock? Stack { get; set; }
        public string? Prerequisites { get; set; }
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
