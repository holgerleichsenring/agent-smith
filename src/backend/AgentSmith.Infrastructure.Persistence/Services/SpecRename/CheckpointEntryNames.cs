using System.Text.RegularExpressions;

namespace AgentSmith.Infrastructure.Persistence.Services.SpecRename;

/// <summary>
/// 2026-10-12-967ah: the current key and assembly-qualified type name of a stored checkpoint entry,
/// from <see cref="SpecRenameTable"/>'s keys and type names. Every renamed full name inside the
/// type name is swapped, generic arguments included.
/// </summary>
public sealed partial class CheckpointEntryNames
{
    /// <summary>The current key and type name, each null when it carries no old name.</summary>
    public (string? Key, string? TypeName) Rewrite(string key, string typeName)
    {
        var newKey = SpecRenameTable.ContextKeys.FirstOrDefault(r => r.Old == key)?.New;
        var newType = FullTypeName().Replace(typeName, match =>
            SpecRenameTable.TypeNames.FirstOrDefault(r => r.Old == match.Value)?.New ?? match.Value);
        return (newKey, newType == typeName ? null : newType);
    }

    // A dotted CLR full name, delimited by anything that is not part of one ('[', ',', ']', '`', '+').
    [GeneratedRegex(@"[A-Za-z_][\w.]*")]
    private static partial Regex FullTypeName();
}
