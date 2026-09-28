using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Infrastructure.Core.Services.Configuration.Retired;

/// <summary>
/// Every configuration key this product has stopped reading, and the one table
/// <see cref="RetiredConfigKeyDetector"/> reads. Retiring a key is one row here: the file loader,
/// the stored-document loader and their advisory findings follow from it.
/// </summary>
public static class RetiredConfigKeys
{
    public static IReadOnlyList<RetiredConfigKey> All { get; } =
    [
        new("trackers.*.parent_link_type", "2026-09-28",
            "Nothing links a filed ticket to a parent any more: an approved cut files one work "
            + "ticket, so there is no child to link."),
    ];
}
