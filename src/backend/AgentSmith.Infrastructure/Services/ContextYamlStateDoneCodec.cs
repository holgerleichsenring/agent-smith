using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace AgentSmith.Infrastructure.Services;

/// <summary>
/// 2026-08-26-31e5: the state.done block of a context.yaml. 2026-11-26-462aa: read out and spliced away — the done
/// index is the context's done.md, a line file git can union-merge, where a YAML block cannot be.
/// <para>
/// The entries are read by a YAML parser in file order, so quoting and escapes are decoded; the block is removed as
/// text, so every other line stays byte for byte. Comment lines inside the block go with it.
/// </para>
/// </summary>
public sealed class ContextYamlStateDoneCodec(ContextYamlBuilders builders) : IContextYamlStateDoneCodec
{
    public ContextYamlDoneExtraction Extract(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        var lines = yaml.Split('\n').ToList();
        var block = ContextStateBlock.Locate(lines);
        if (block.DoneLine == ContextStateBlock.Missing) return ContextYamlDoneExtraction.None;
        if (!TryEntries(yaml, out var entries, out var error)) return ContextYamlDoneExtraction.Failed(error!);

        lines.RemoveRange(block.DoneLine, ContextStateBlock.EndOfOwnLines(lines, block.DoneLine) - block.DoneLine);
        var stateEnd = ContextStateBlock.EndOfOwnLines(lines, block.StateLine);
        if (lines.Skip(block.StateLine + 1).Take(stateEnd - block.StateLine - 1).All(ContextStateBlock.IsBlank))
            lines.RemoveRange(block.StateLine, stateEnd - block.StateLine);

        var result = string.Join('\n', lines);
        return Reparses(result, out error)
            ? new ContextYamlDoneExtraction(result, entries, null)
            : ContextYamlDoneExtraction.Failed(error!);
    }

    private static bool TryEntries(string yaml, out IReadOnlyList<KeyValuePair<string, string>> entries, out string? error)
    {
        entries = [];
        try
        {
            var stream = new YamlStream();
            using (var reader = new StringReader(yaml)) stream.Load(reader);
            if (stream.Documents.Count == 0 || stream.Documents[0].RootNode is not YamlMappingNode root
                || !root.Children.TryGetValue(new YamlScalarNode("state"), out var state) || state is not YamlMappingNode stateMap
                || !stateMap.Children.TryGetValue(new YamlScalarNode("done"), out var done))
            {
                error = null;
                return true;
            }
            if (done is YamlScalarNode { Value: null or "" }) { error = null; return true; }
            if (done is not YamlMappingNode doneMap) { error = "state.done is not a mapping"; return false; }
            var read = new List<KeyValuePair<string, string>>();
            foreach (var (key, value) in doneMap.Children)
            {
                if (key is not YamlScalarNode id || value is not YamlScalarNode scalar)
                {
                    error = $"state.done.{key} is not an id with a line of text";
                    return false;
                }
                read.Add(new(id.Value ?? string.Empty, OneLine(scalar.Value)));
            }
            entries = read;
            error = null;
            return true;
        }
        catch (YamlException ex)
        {
            error = $"the context.yaml does not parse: {ex.Message}";
            return false;
        }
    }

    private static string OneLine(string? value) =>
        (value ?? string.Empty).Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ');

    private bool Reparses(string yaml, out string? error)
    {
        try
        {
            builders.Deserializer.Deserialize<Dictionary<object, object?>>(yaml);
            error = null;
            return true;
        }
        catch (YamlException ex)
        {
            error = $"the context.yaml without state.done no longer parses: {ex.Message}";
            return false;
        }
    }
}
