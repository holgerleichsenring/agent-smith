using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services;

/// <summary>
/// Static helper for the agent-smith work-branch naming convention.
/// Single-segment (<c>agent-smith/&lt;ticketId&gt;</c>) when no project context is provided —
/// sufficient for one-repo-per-ticket-system deployments (the AAD-DEV pattern).
/// Future deployments can pass platform + projectName for the hierarchical
/// <c>agent-smith/&lt;platform&gt;/&lt;projectSlug&gt;/&lt;ticketId&gt;</c> form.
/// </summary>
public static class TicketBranchNamer
{
    public const string Prefix = "agent-smith";
    private const int MaxSlugChars = 64;
    private const int MaxBranchChars = 200;
    private static readonly Regex NonAlnum = new(@"[^a-z0-9]+", RegexOptions.Compiled);

    /// <summary>Single-segment form for one-repo-per-ticket setups.</summary>
    public static BranchName Compose(TicketId ticketId) =>
        new($"{Prefix}/{ticketId.Value}");

    /// <summary>Hierarchical form. Throws ConfigurationException when projectName slugifies to empty.</summary>
    public static BranchName Compose(string platform, string projectName, TicketId ticketId)
    {
        if (string.IsNullOrWhiteSpace(projectName))
            throw new ConfigurationException("projectName must not be empty");
        var platformSlug = Slugify(platform);
        var projectSlug = Slugify(projectName);
        if (string.IsNullOrEmpty(projectSlug))
            throw new ConfigurationException(
                $"projectName '{projectName}' produced empty slug after sanitization");
        if (projectSlug.Length > MaxSlugChars)
            projectSlug = TruncateWithHash(projectSlug);
        var branch = $"{Prefix}/{platformSlug}/{projectSlug}/{ticketId.Value}";
        if (branch.Length > MaxBranchChars)
            throw new ConfigurationException(
                $"Composed branch name exceeds {MaxBranchChars} chars: '{branch}'");
        return new BranchName(branch);
    }

    private static string Slugify(string input) =>
        NonAlnum.Replace(input.ToLowerInvariant(), "-").Trim('-');

    private static string TruncateWithHash(string slug)
    {
        var bytes = SHA1.HashData(Encoding.UTF8.GetBytes(slug));
        var hash = Convert.ToHexString(bytes)[..7].ToLowerInvariant();
        var head = slug[..(MaxSlugChars - 8)];
        return $"{head}-{hash}";
    }
}
