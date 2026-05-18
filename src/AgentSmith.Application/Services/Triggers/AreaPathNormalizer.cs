namespace AgentSmith.Application.Services.Triggers;

/// <summary>
/// p0140a: Azure DevOps area paths come in two forms — the operator may write either
/// <c>ContosoMain\Billing</c> (the ADO-native form, but requires double-escape in YAML
/// quoted strings) or <c>ContosoMain/Billing</c> (the safer YAML form). We normalise both
/// to the backslash form for prefix matching so the operator can pick either style.
///
/// Prefix matching is hierarchical: parent <c>ContosoMain\Billing</c> matches itself
/// AND <c>ContosoMain\Billing\Invoicing</c>, but NOT the sibling
/// <c>ContosoMain\BillingOther</c> — the boundary must fall on a real path segment.
/// </summary>
internal static class AreaPathNormalizer
{
    public static string? Normalize(string? path)
    {
        if (string.IsNullOrEmpty(path)) return path;
        var normalised = path.Replace('/', '\\').TrimEnd('\\');
        return normalised.Length == 0 ? null : normalised;
    }

    public static bool IsPrefix(string? parent, string? child)
    {
        var p = Normalize(parent);
        var c = Normalize(child);
        if (string.IsNullOrEmpty(p) || string.IsNullOrEmpty(c)) return false;
        if (string.Equals(p, c, StringComparison.OrdinalIgnoreCase)) return true;
        return c.StartsWith(p + '\\', StringComparison.OrdinalIgnoreCase);
    }
}
