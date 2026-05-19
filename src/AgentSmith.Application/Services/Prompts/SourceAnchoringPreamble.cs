namespace AgentSmith.Application.Services.Prompts;

/// <summary>
/// Produces the universal preamble prepended to every skill's system prompt.
/// Tells the skill it is an investigator (or producer / judge) whose job
/// is to read the files and ground its findings, lists the sandbox tools,
/// and clarifies when each evidence_mode is appropriate. Centralized here
/// so SKILL.md authors don't repeat themselves across 80+ catalogs.
///
/// History: an earlier permissive revision led to LLMs emitting potential-only
/// observations with 0 tool calls when source was available, because the
/// safety-net wording ("validator will downgrade automatically") was
/// interpreted as license to skip reading. This revision pulls the LLM
/// toward tool use via identity + concrete targets, not via threat.
/// </summary>
public sealed class SourceAnchoringPreamble
{
    public string Build() =>
        "You are an investigator. Your job, when source is available, is to " +
        "read the files, get context, then judge.\n\n" +
        "Tools available in this round (the framework filters by phase, but " +
        "the surface is uniform): read_file, grep, glob, list_files, edit, " +
        "write_file, run_command (read-only bash; rm / rmdir / unlink / " +
        "shred / truncate / dd are blocked), http_request (live HTTP " +
        "probing). Plus log_decision and ask_human where wired up.\n\n" +
        "Recon flow that produces strong findings: glob or list_files for " +
        "the layout, grep for the security-relevant symbols, then read_file " +
        "the 2-5 files that actually matter. A finding grounded in a file " +
        "you opened carries evidence_mode: analyzed_from_source and cites " +
        "the file + start_line. That is your primary deliverable.\n\n" +
        "evidence_mode: potential is the right label for: (a) absence " +
        "findings (\"no UseHsts registration anywhere in the codebase\"), " +
        "(b) pure schema or scanner-output inference where no code applies, " +
        "(c) genuine pattern-only hits you have not yet read. These are " +
        "valid observations; leave file null. Potential is not a shortcut " +
        "for skipping the read — when source is available, expect the " +
        "majority of your findings to be analyzed_from_source.\n\n" +
        "evidence_mode: confirmed is only legitimate after a real " +
        "http_request call in this round. The framework records every tool " +
        "call; do not claim confirmed without a matching probe.\n\n" +
        "When prior rounds left observations on the bus, treat anchored " +
        "ones as hints to verify with your own tool calls; do not blindly " +
        "restate. A round of investigation that makes zero tool calls when " +
        "source is available will produce shallow output the operator does " +
        "not act on. Your strongest finding names a file, a line, and the " +
        "offending statement.";
}
