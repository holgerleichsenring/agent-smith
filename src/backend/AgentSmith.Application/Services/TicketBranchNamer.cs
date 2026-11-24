using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services;

/// <summary>
/// Static helper for the agent-smith work-branch naming convention.
/// Single-segment (<c>agent-smith/&lt;ticketId&gt;</c>) when no project context is provided —
/// sufficient for one-repo-per-ticket-system deployments (the reference deployment pattern).
/// Future deployments can pass platform + projectName for the hierarchical
/// <c>agent-smith/&lt;platform&gt;/&lt;projectSlug&gt;/&lt;ticketId&gt;</c> form.
/// </summary>
public static class TicketBranchNamer
{
    public const string Prefix = AgentSmith.Contracts.Providers.WorkBranches.Prefix;

    /// <summary>2026-11-24-f9c5b: the prefix of every series branch — two segments, so <see cref="TicketOf"/> never reads one
    /// as a ticket's branch.</summary>
    public const string SeriesPrefix = AgentSmith.Contracts.Providers.WorkBranches.SeriesPrefix;

    /// <summary>The branch an init-project run works on, under the same prefix as every ticket run.</summary>
    public const string InitBranch = Prefix + "/init";
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

    /// <summary>2026-11-24-f9c5b: the branch a series' tickets integrate on.</summary>
    public static BranchName Series(string seriesRunId) =>
        string.IsNullOrWhiteSpace(seriesRunId)
            ? throw new ArgumentException("A series branch needs the series' id.", nameof(seriesRunId))
            : new BranchName(SeriesPrefix + seriesRunId.Trim());

    /// <summary>2026-11-24-f9c5b: whether a branch is a series branch; a <c>refs/heads/</c> prefix is ignored.</summary>
    public static bool IsSeries(string? branch) => AgentSmith.Contracts.Providers.WorkBranches.IsSeries(branch);

    /// <summary>
    /// 2026-11-24-f9c5b: the branch a run's parent names — a parent already under agent-smith/ (a series branch) is that
    /// branch, any other is a ticket whose branch is agent-smith/&lt;ticket&gt;. Every reader of a run's base composes
    /// through here, so the series branch is one more parent rather than a second base path.
    /// </summary>
    public static BranchName RungOf(string parent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parent);
        var trimmed = parent.Trim();
        return AgentSmith.Contracts.Providers.WorkBranches.Owns(trimmed) ? new BranchName(trimmed) : Compose(new TicketId(trimmed));
    }

    /// <summary>
    /// 2026-10-08-e8b9c: the ticket a pull request's head branch belongs to — <c>agent-smith/</c> plus
    /// exactly ONE segment that is not <c>init</c>; null for anything else (a foreign branch, the init
    /// branch, the unused hierarchical form). A <c>refs/heads/</c> prefix is ignored.
    /// </summary>
    public static string? TicketOf(string? headRef)
    {
        var name = BranchRef.ShortName(headRef);
        if (name is null || !name.StartsWith(Prefix + "/", StringComparison.Ordinal)) return null;
        var ticket = name[(Prefix.Length + 1)..];
        return ticket.Length == 0 || ticket.Contains('/') || ticket == "init" ? null : ticket;
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
