namespace AgentSmith.Application.Services.Prompts;

/// <summary>
/// Produces the universal preamble prepended to every skill's system prompt.
/// Lists the sandbox tools available to investigators / producers / judges,
/// states the evidence_mode rule, and clarifies that the runtime DOWNGRADES
/// (no longer drops, post-PR #171) mis-labeled analyzed_from_source
/// observations. Centralizing the rule here means SKILL.md authors don't
/// need to repeat tool listings in every catalog entry — see p0151's
/// "no new SKILL.md format" decision.
/// </summary>
public sealed class SourceAnchoringPreamble
{
    public string Build() =>
        "You have these sandbox tools (always available; the framework " +
        "filters by phase, but the tool surface is uniform across catalogs): " +
        "read_file, grep, glob, list_files, edit, write_file, run_command " +
        "(read-only bash; rm / rmdir / unlink / shred / truncate / dd are " +
        "blocked), http_request (live HTTP probing). Plus log_decision and " +
        "ask_human in pipelines that wire them up. Use them — broad recon " +
        "first (glob / grep with a pipe to head / list_files), then targeted " +
        "reads. A vanilla LLM with raw bash will out-perform you if you fall " +
        "back to schema-only inference.\n\n" +
        "evidence_mode contract: " +
        "analyzed_from_source means you opened the cited file via read_file " +
        "in THIS skill round (the runtime tracks a ReadSet and downgrades " +
        "unverified claims to potential automatically — your description " +
        "still surfaces, but the strong label is reserved for genuine reads). " +
        "confirmed means you used http_request and observed the live behavior. " +
        "potential covers schema / scanner / design inference and " +
        "absence-of-thing findings (\"no UseHsts anywhere\"); these do NOT " +
        "require a file anchor — set file to null. Pick the matching anchor " +
        "field per evidence: file + start_line for source, api_path for " +
        "endpoint behavior, schema_name for schema-only, template_id (in the " +
        "description) for scanner-derived. When prior rounds left observations " +
        "on the bus, treat any anchored prior observation as a hint to verify " +
        "with your own tool calls; do not blindly restate.";
}
