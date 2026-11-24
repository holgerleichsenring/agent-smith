using System.Globalization;

namespace AgentSmith.Contracts.Models;

/// <summary>
/// 2026-11-23-933dg: whose a stored secret value is — the installation's (a catalog secret) or one PoC's (a variable of
/// that PoC). <see cref="Text"/> is what the store's Scope column holds: 'installation' or 'poc:&lt;id&gt;'.
/// </summary>
public sealed record SecretScope
{
    public const string InstallationText = "installation";
    private const string PocPrefix = "poc:";

    /// <summary>The Scope column's width: 'poc:' plus a long id fits.</summary>
    public const int MaxLength = 32; // not a limit: the column width 'poc:' + a 64-bit id fits in

    /// <summary>The stored Name column's width (StoredSecrets.Name), so also the longest PoC variable name.</summary>
    public const int NameMaxLength = 200; // not a limit: the StoredSecrets.Name column width

    private SecretScope(string text, long? pocId) => (Text, PocId) = (text, pocId);

    public static SecretScope Installation { get; } = new(InstallationText, null);

    public static SecretScope Poc(long pocId) => new(PocPrefix + pocId.ToString(CultureInfo.InvariantCulture), pocId);

    public string Text { get; }

    /// <summary>The PoC whose variable this is; null for the installation.</summary>
    public long? PocId { get; }

    public bool IsInstallation => PocId is null;

    /// <summary>The scope a stored Scope column names; null when it names none.</summary>
    public static SecretScope? Parse(string? text)
    {
        if (string.Equals(text, InstallationText, StringComparison.Ordinal)) return Installation;
        return text is not null && text.StartsWith(PocPrefix, StringComparison.Ordinal)
            && long.TryParse(text[PocPrefix.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var id)
            ? Poc(id) : null;
    }

    public override string ToString() => Text;
}
