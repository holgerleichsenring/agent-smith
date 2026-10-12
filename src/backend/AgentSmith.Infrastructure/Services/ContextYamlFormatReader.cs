using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Infrastructure.Services;

/// <summary>
/// 2026-10-12-fbbdb: turns the deserialised <c>format:</c> block into the contract the
/// discovery carries. A block without a command, or whose command lacks the files
/// placeholder, formats nothing a spec changed, so it is dropped here rather than travelling
/// as a command that would format the whole repository or nothing at all.
/// </summary>
internal static class ContextYamlFormatReader
{
    public static ContextYamlFormat? Read(ContextYamlReadShape.FormatBlock? block)
    {
        var command = block?.Command?.Trim();
        if (string.IsNullOrEmpty(command)
            || !command.Contains(ContextYamlFormat.FilesPlaceholder, StringComparison.Ordinal))
            return null;
        var whenPresent = block!.WhenPresent?.Trim();
        return new ContextYamlFormat(command, string.IsNullOrEmpty(whenPresent) ? null : whenPresent);
    }
}
