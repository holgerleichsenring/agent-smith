using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using YamlDotNet.Core;

namespace AgentSmith.Infrastructure.Services;

/// <summary>
/// p0375: reads and upserts the <c>registry_auth</c> section of a context.yaml
/// through the shared <see cref="ContextYamlBuilders"/> configuration. Upsert
/// round-trips the WHOLE document as a generic map, so sections the typed
/// <see cref="ContextYamlDocument"/> does not model (state, integrations, …)
/// survive the rewrite untouched.
/// </summary>
public sealed class ContextYamlRegistryAuthCodec : IContextYamlRegistryAuthCodec
{
    public ContextYamlRegistryAuth? Read(string yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml)) return null;
        ReadShape? doc;
        try
        {
            doc = ContextYamlBuilders.Deserializer.Deserialize<ReadShape>(yaml);
        }
        catch (YamlException)
        {
            // Unparseable operator YAML is surfaced by the summary parse path
            // (ContextYamlSerializer.Parse); here it just means "no section".
            return null;
        }

        var files = doc?.RegistryAuth?.Files?
            .Where(f => !string.IsNullOrWhiteSpace(f.Path) && !string.IsNullOrEmpty(f.Content))
            .Select(f => new ContextYamlRegistryAuthFile(f.Path!, f.Content!))
            .ToList();
        return files is { Count: > 0 } ? new ContextYamlRegistryAuth(files) : null;
    }

    public string Upsert(string yaml, ContextYamlRegistryAuth section)
    {
        ArgumentNullException.ThrowIfNull(section);
        var root = string.IsNullOrWhiteSpace(yaml)
            ? new Dictionary<object, object?>()
            : ContextYamlBuilders.Deserializer.Deserialize<Dictionary<object, object?>>(yaml)
              ?? new Dictionary<object, object?>();
        root["registry_auth"] = Map(section);
        return ContextYamlBuilders.Serializer.Serialize(root);
    }

    private static Dictionary<string, object?> Map(ContextYamlRegistryAuth section) => new()
    {
        ["files"] = section.Files
            .Select(f => new Dictionary<string, object?> { ["path"] = f.Path, ["content"] = f.Content })
            .ToList(),
    };

    private sealed class ReadShape
    {
        public RegistryAuthBlock? RegistryAuth { get; set; }
    }

    private sealed class RegistryAuthBlock
    {
        public List<FileBlock>? Files { get; set; }
    }

    private sealed class FileBlock
    {
        public string? Path { get; set; }
        public string? Content { get; set; }
    }
}
