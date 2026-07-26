using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Reads and upserts the <c>registry_auth</c> section of a context.yaml
/// (p0375) through the same YamlDotNet builder configuration as
/// <see cref="IContextYamlSerializer"/> — the p0193 one-builder rule. Upsert
/// round-trips the WHOLE document as a generic map so every other section
/// (arch, quality, state, …) survives the rewrite untouched.
/// </summary>
public interface IContextYamlRegistryAuthCodec
{
    /// <summary>The registry_auth section, or null when absent / unparseable / empty.</summary>
    ContextYamlRegistryAuth? Read(string yaml);

    /// <summary>Returns the document with the registry_auth section added or replaced.</summary>
    string Upsert(string yaml, ContextYamlRegistryAuth section);
}
