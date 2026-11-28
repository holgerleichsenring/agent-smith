using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Infrastructure.Core.Services.Configuration.Retired;

/// <summary>
/// Every configuration key this product has stopped reading, and the one table
/// <see cref="RetiredConfigKeyDetector"/> reads. Retiring a key is one row here: the file loader,
/// the stored-document loader and their advisory findings follow from it.
/// </summary>
public static class RetiredConfigKeys
{
    private const string OrchestratorImage =
        "No run spawns an orchestrator container any more: the pipeline runs in the server. "
        + "deployment.version still pins the sandbox agent image.";

    private const string SkillCallRuntimeGone =
        "It was read only by the skill-call runtime, which nothing had used since p0312, so it bounded "
        + "nothing; the run's cost cap is the budget (2026-11-17-274cb). Remove the key from agentsmith.yml.";

    private const string SpecReviewGone =
        "No step of a run reviews a spec any more: the master builds the spec it has and briefs a reviewer itself when it "
        + "wants one (2026-11-18-9c1cb, 2026-11-19-3e10). Remove the key from agentsmith.yml.";

    private const string ReviewReplaces =
        "One project setting, review: end | each | none (projects.<name>.review, default end), says when a person looks "
        + "(2026-11-25-9992i). A stored configuration was moved by the data migration of `database migrate`; in "
        + "agentsmith.yml, set review: and remove the key.";

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
        new("orchestrator.registry", "2026-09-28", OrchestratorImage),
        new("orchestrator.version", "2026-09-28", OrchestratorImage),
        new("projects.*.orchestrator.registry", "2026-09-28", OrchestratorImage),
        new("projects.*.orchestrator.version", "2026-09-28", OrchestratorImage),
        new("projects.*.orchestrator.resources", "2026-09-28",
            "No run spawns an orchestrator pod any more: the pipeline runs in the server, and a "
            + "run's footprint is its sandboxes. Size those under sandbox.resources."),
        new("pipeline_storage", "2026-10-02",
            "It set the lifetime of in-flight run artifacts in Redis, and nothing stores them there "
            + "any more: a run's markdown lives in the database (2026-10-02-5ab2f)."),
        new("limits.max_output_tokens_per_skill_call", "2026-11-12",
            "It was never enforced: the input-token and cost caps are the budget (2026-11-12-cd4cl)."),
        new("limits.max_tool_calls_per_investigator", "2026-11-17", SkillCallRuntimeGone),
        new("limits.max_tool_calls_per_verifier", "2026-11-17", SkillCallRuntimeGone),
        new("limits.max_llm_calls_per_skill", "2026-11-17", SkillCallRuntimeGone),
        new("limits.max_input_tokens_per_skill_call", "2026-11-17", SkillCallRuntimeGone),
        new("limits.max_concurrent_skill_calls", "2026-11-17", SkillCallRuntimeGone),
        new("limits.max_skills_per_phase", "2026-11-17",
            "Nothing read it after triage retired (p0312); the skills a stage runs are its master's choice."),
        new("agents.*.reminder_drift_editless_iterations", "2026-11-18",
            "Nothing reminds the master on reads without an edit any more: its loop is steered by mechanical facts only (2026-11-18-9c1cc)."),
        new("agents.*.verdict_owed_after_iterations", "2026-11-18",
            "Nothing demands the master's report or ends its pass when its checklist is complete (2026-11-18-9c1cc)."),
        new("limits.max_dialog_sub_agent_loop_iterations", "2026-11-12",
            "A design turn's child reads its ceiling from agents.<name>.dialog_sub_agent_loop_iterations (2026-11-12-cd4cl)."),
        new("spec_review.design_turn", "2026-11-18",
            "No review runs after a Work it out turn: its master owns the draft and commissions a review when it wants "
            + "one (2026-11-18-9c1ca)."),
        new("spec_review.rounds", "2026-11-19", SpecReviewGone),
        new("spec_review.reviewers", "2026-11-19", SpecReviewGone),
        new("spec_review.max_concurrent", "2026-11-19", SpecReviewGone),
        new("spec_review.budget_share_percent", "2026-11-19", SpecReviewGone),
        new("spec_review.attack_look_floor", "2026-11-19", SpecReviewGone),
        new("projects.*.merge.approval_auth", "2026-11-25",
            "agent-smith no longer approves pull requests: a review a host requires is a person's (2026-11-25-9992h)."),
        new("spec_review.series_contract_max_chars", "2026-11-19",
            "Nothing shows the series contract to a reviewer any more; the reviewer that read it was never wired (2026-11-19-4176)."),
        new("registries", "2026-11-25",
            "Private package feeds are configured per project now, under projects.<name>.registries (2026-11-25-9992d). "
            + "A stored configuration was moved by the data migration of `database migrate`, which copied the list into "
            + "every project that had none of its own; in agentsmith.yml, move the entries under each project."),
        new("projects.*.merge.when", "2026-11-25", ReviewReplaces),
        new("projects.*.merge.series_when", "2026-11-25", ReviewReplaces),
        new("projects.*.merge.respect_branch_protection", "2026-11-25",
            "The host's branch protection is a limit of the environment, never a setting: a merge on green arms the host's "
            + "auto-merge and its rules decide. " + ReviewReplaces),
        new("projects.*.merge.refresh_open_pull_requests", "2026-11-25",
            "Nothing merges a landed base into conflict-free pull requests any more; a conflicting one is still refreshed. "
            + ReviewReplaces),
        new("queue.consume_block_seconds", "2026-11-28",
            "The queue consumer never read it, so it bounded nothing (2026-11-28-34d9d). Remove the key."),
        new("trackers.*.polling.jitter_percent", "2026-11-28",
            "The poller never read it: one sleep serves every polling tracker and varies by a fixed ±10% (2026-11-28-34d9d). "
            + "Remove the key."),
        new("agents.*.compaction.threshold_iterations", "2026-11-28",
            "Compaction fires on token pressure only (max_context_tokens x max_context_tokens_trigger_ratio); the "
            + "iteration count did nothing since p0357. Remove the key."),
        new("projects.*.polling", "2026-11-28",
            "Polling belongs to the tracker entry, trackers.<name>.polling, and a project's own block was ignored since "
            + "p0140c. Set polling on the project's tracker and remove the block."),
    ];
}
