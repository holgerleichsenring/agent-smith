using System.Security.Claims;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Server.Security;

/// <summary>
/// 2026-08-26-7a51: the roles an administrator granted a PERSON here, resolved against
/// the claim each grant was written for.
/// <para>
/// A grant carries its claim because the name claim is bootstrap and can change: written
/// under <c>preferred_username</c> and later read under <c>email</c>, one value can name
/// two people. So a grant resolves only while its claim IS the configured name claim, and
/// one that no longer matches grants nothing and says so through the findings channel
/// rather than failing silently.
/// </para>
/// <para>
/// The comparison is ORDINAL. A name-claim value is an identifier the directory chose,
/// and case-folding an identifier is how <c>Alice</c> and <c>alice</c> stop being a
/// question anybody can answer.
/// </para>
/// </summary>
internal sealed class PersonGrantReader
{
    private readonly List<PersonGrant> _matching = [];
    private readonly List<string> _findings = [];

    public PersonGrantReader(RoleMappingConfig mapping, string nameClaim)
    {
        foreach (var grant in mapping.PersonGrants)
        {
            if (string.Equals(grant.Claim, nameClaim, StringComparison.Ordinal)) _matching.Add(grant);
            else _findings.Add(Stale(grant, nameClaim));
        }
    }

    /// <summary>What is wrong with the grants as written, said where an operator can read it.</summary>
    public IReadOnlyList<string> Findings => _findings;

    /// <summary>The roles this caller was granted here. Empty when nothing names them.</summary>
    public IReadOnlyList<string> Roles(ClaimsPrincipal caller)
    {
        var value = caller.Identity?.Name;
        return value is null or "" ? [] : Roles(value);
    }

    /// <summary>The roles granted to one name-claim value, for a surface that has no principal.</summary>
    public IReadOnlyList<string> Roles(string nameValue) =>
        [.. _matching
            .Where(g => string.Equals(g.Value, nameValue, StringComparison.Ordinal))
            .SelectMany(g => g.Roles)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.Ordinal)];

    private static string Stale(PersonGrant grant, string nameClaim) =>
        $"The grant for '{grant.Value}' was written against the claim '{grant.Claim}', but "
        + $"callers are named by '{nameClaim}' here, so it grants nothing. Grant the role "
        + "again against the claim in force, or point the name claim back.";
}
