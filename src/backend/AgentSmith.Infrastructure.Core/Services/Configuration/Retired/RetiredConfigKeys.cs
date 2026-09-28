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
        new("projects.*.pipelines.*.confidence_threshold", "2026-09-28",
            "Nothing downgrades a blocking observation any more, so the threshold that tuned it is "
            + "not read. Remove the key from agentsmith.yml; in a stored configuration, export, "
            + "edit and import it again."),
        new("agents.*.compaction.summary_model", "2026-09-28",
            "Compaction summarizes with the agent's summarization role; this model was never "
            + "consulted after the compactor was rebuilt. Name that role under models: instead."),
        new("agents.*.compaction.deployment_name", "2026-09-28",
            "Compaction summarizes with the agent's summarization role, deployment included; "
            + "this override was never consulted after the compactor was rebuilt."),
        new("tool_runner.namespace", "2026-09-28",
            "Scanners run on the Docker or Podman engine or as local processes; no tool runner reads a "
            + "Kubernetes namespace."),
        new("tool_runner.image_pull_policy", "2026-09-28",
            "No tool runner reads a pull policy; the engine pulls an image it does not have."),
        new("agents.*.parallelism", "2026-09-28",
            "Skill rounds no longer run in batches, so there is nothing for it to bound."),
    ];
}
