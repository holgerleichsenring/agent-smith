using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using YamlDotNet.Core;
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
    public ContextYamlParseResult Parse(string yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml)) return ContextYamlParseResult.Empty();
        Shape? doc;
        try
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();
            doc = deserializer.Deserialize<Shape>(yaml);
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
            new ContextYamlSummary(doc.Meta.Workdir.Trim(), doc.Stack?.Lang?.Trim()));
    }

    private static string FormatYamlError(YamlException ex, string yaml)
    {
        // YamlException.Message is generic ("found character that cannot
        // start any token"). Position lives on Start (Mark). We prepend
        // line/col so operators can jump straight to the bad line, and
        // inspect the source line to add a targeted hint for the most
        // common cause we've actually hit: unquoted @-prefixed scoped
        // package names in `sdks:` / `frameworks:` lists.
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
