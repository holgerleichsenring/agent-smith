using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// What an account has to SAY about itself, as lines with the level they deserve.
/// <para>
/// Lifted out of <see cref="PhaseAccounting"/> in p0434, which needed the room to stop
/// losing a pull request to a provider blip. It composes lines and does not write them:
/// a static that took the logger would be a service without a constructor, which the
/// no-static-state rule refuses — and it is right to, because the caller already has one.
/// </para>
/// </summary>
internal static class SpecAccountLog
{
    internal static IReadOnlyList<(LogLevel Level, string Message)> Lines(SpecAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);
        if (account.Problem is not null)
            return [(LogLevel.Warning, $"No account could be taken — {account.Problem}")];
        if (account.Delivered)
            return [(LogLevel.Information,
                $"All {account.Criteria.Count} ratified criteria are accounted for")];
        return
        [
            .. account.Outstanding.Select(o => (LogLevel.Warning,
                $"OUTSTANDING — {o.Criterion}{(o.Note is null ? string.Empty : $" ({o.Note})")}")),
        ];
    }
}
