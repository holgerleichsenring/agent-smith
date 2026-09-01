namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-09-01-379a: what the model is told a context document may carry. Extracted from
/// <see cref="WriteContextYamlToolHost"/>, which attempts the write — this is prompt
/// content, and it grew every time the document gained a field while the host's own
/// responsibility did not change.
/// </summary>
internal static class WriteContextYamlToolDescription
{
    public const string Document =
        "Document object: { meta: { workdir, type?: [archetype,…], purpose? }, " +
        "stack?: { lang?, image?, resources? }, " +
        "verify?: [ { label, command, when_present? }, … ], " +
        "verify_derived_from?: { files: [path,…] }, " +
        "probe?: { target, command }, " +
        "arch?: object, quality?: object, behavior?: object }. " +
        "Do NOT restate what the repository already states about itself — the build " +
        "file's frameworks, versions and packages, the workflow's CI platform, the " +
        "folder names as layers. Those are dropped. State what somebody DECIDED " +
        "(meta.purpose, quality.limits, behavior) and what the orchestrator ACTS ON " +
        "(meta.workdir, stack.lang, stack.image). " +
        "meta.workdir is REQUIRED — '.' for single-stack, otherwise the sub-tree path. " +
        "stack.image is REQUIRED whenever a stack is present — the exact toolchain Docker image " +
        "whose runtime can BOTH build AND run this stack's tests (e.g. mcr.microsoft.com/dotnet/sdk:8.0, " +
        "node:20-bookworm); it must come from a registry the operator trusts and must carry git, " +
        "because the repository is cloned inside it. " +
        // p0332: resources demoted to the exception — the defaults fit
        // almost every stack; agents must stop sizing every context.yaml.
        "stack.resources is NORMALLY OMITTED — the platform defaults fit almost every stack, " +
        "including real dotnet/Roslyn and npm builds. Declare it only for a defensible outlier: " +
        "a build that DEMONSTRABLY needs more than the default (e.g. it OOM-killed or you measured " +
        "the peak). If you declare it, provide ALL FOUR Kubernetes quantities { cpu_request, " +
        "cpu_limit, memory_request, memory_limit } — a partial block is refused — and values above " +
        "the hard ceiling (cpu '2', memory '6Gi') are clamped down to it. " +
        // 2026-08-31-26d4: the gate the repository owns, ahead of anything a
        // model emits for a single run.
        "verify is the ORDERED list of commands that prove a change in this context holds — " +
        "each { label, command, when_present? }, run at this context's workdir, stopping at " +
        "the first non-zero exit. Every command must be able to FAIL: a declared 'echo ...' " +
        "or 'true' stops the run at resolution. Use when_present for a stage that only means " +
        "something when a path exists; an absent path skips that stage instead of reddening " +
        "it. Omit verify for a .NET tree — its entry point is discovered from files that exist. " +
        // 2026-09-01-e14d: adoption, not invention — and a record of what was adopted.
        "DERIVE those commands from what this repository already runs: its CI definition " +
        "(azure-pipelines.yml, .github/workflows/*, .gitlab-ci.yml, Jenkinsfile), its " +
        "Makefile and scripts, its manifests and task runners — the same reading you do " +
        "to work out the build command. Name the files you read them out of in " +
        "verify_derived_from.files (paths relative to meta.workdir); the framework " +
        "hashes those files itself, so send no hash. A repository whose pipeline you " +
        "could not find gets NO verify block and no verify_derived_from — an invented " +
        "gate disagrees with the one the estate actually runs. " +
        // 2026-09-01-379a: the question the injected credentials exist for.
        "probe is the ONE command that asks whether this context's TARGET " +
        "ENVIRONMENT answers — { target, command }, run at this context's " +
        "workdir before the coding agent starts. Name the target in your own " +
        "words; reference an injected credential by name ($VAR), never by " +
        "value. Declare it only when work here depends on a live target; a " +
        "repository that needs none omits the block.";
}
