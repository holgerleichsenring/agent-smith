using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using YamlDotNet.Core;

namespace AgentSmith.Infrastructure.Services;

/// <summary>
/// p0193: one YamlDotNet builder configuration shared by emit + consume
/// (see <see cref="ContextYamlBuilders"/>). Parse-failures from agent-written
/// context.yaml become unrepresentable — the writer is the same code as the
/// reader, applied via Serialize().
/// </summary>
public sealed class ContextYamlSerializer(ContextYamlBuilders builders) : IContextYamlSerializer
{
    private readonly YamlDotNet.Serialization.ISerializer _yamlSerializer = builders.Serializer;

    private readonly YamlDotNet.Serialization.IDeserializer _yamlDeserializer = builders.Deserializer;

    public string Serialize(ContextYamlDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (string.IsNullOrWhiteSpace(document.Meta?.Workdir))
            throw new InvalidOperationException(
                "ContextYamlDocument.Meta.Workdir is required (p0161).");
        return _yamlSerializer.Serialize(document);
    }

    public ContextYamlParseResult Parse(string yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml)) return ContextYamlParseResult.Empty();
        ContextYamlReadShape? doc;
        try
        {
            doc = _yamlDeserializer.Deserialize<ContextYamlReadShape>(yaml);
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
                doc.Prerequisites?.Trim(),
                doc.Stack?.Image?.Trim(),
                MapResources(doc.Stack?.Resources),
                // p0331: meta.purpose feeds the ScopeRepos ticket→repo classifier.
                doc.Meta.Purpose?.Trim(),
                // 2026-08-31-26d4: the declared verify stages, which the gate runs ahead
                // of anything a model emitted for this run.
                ContextYamlVerifyReader.Read(doc.Verify),
                // 2026-09-01-e14d: and what those stages were derived from, so the run can
                // re-hash it and say when the declaration's source has moved.
                ContextYamlVerifyReader.ReadDerivation(doc.VerifyDerivedFrom)));
    }

    // p0268: pass the raw four fields through UNPARSED. Trimming only; the
    // SandboxResourceResolver is the single gate that validates (parse-as-quantity,
    // all-or-none) and either maps the block to ResourceLimits or rejects it whole.
    // Returning null when the block is entirely empty keeps "no resources" distinct
    // from "a present block" so the resolver only warns on a present-but-invalid one.
    private static ContextYamlStackResources? MapResources(
        ContextYamlReadShape.ResourcesBlock? block)
    {
        if (block is null) return null;
        var mapped = new ContextYamlStackResources(
            block.CpuRequest?.Trim(), block.CpuLimit?.Trim(),
            block.MemoryRequest?.Trim(), block.MemoryLimit?.Trim());
        var allEmpty = string.IsNullOrEmpty(mapped.CpuRequest)
            && string.IsNullOrEmpty(mapped.CpuLimit)
            && string.IsNullOrEmpty(mapped.MemoryRequest)
            && string.IsNullOrEmpty(mapped.MemoryLimit);
        return allEmpty ? null : mapped;
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
}
