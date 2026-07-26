---
name: feedback_skills_dont_overconstrain_llm
description: "agent-smith skill catalog must give the LLM raw exploration tools (bash, parallel reads, sub-agent delegation) — narrow Grep/ReadFile-only tool-sets produce worse output than a vanilla Claude with bash+read"
metadata:
  type: feedback
status: proposed
---
When designing skill tool-sets and SKILL.md prompts in agent-smith / agent-smith-skills:
- The LLM is NOT the problem. A vanilla Claude with `Bash` + `Read` + `Agent(Explore)` reliably finds vulnerabilities that the structured api-security-scan skills miss (e.g. IUserPermissionCache scope-lifetime audit, ExceptionMiddleware stack-trace leak, hardcoded secrets in `pipelines/*.yml`, missing `UseHsts`/`TokenValidationParameters`, etc.).
- The skills miss those findings because they only get `Grep` / `ListFiles` / `ReadFile` (or sometimes `RunCommand`), no `Bash` with pipes/find/head, no parallel tool-calls in one turn, no sub-agent delegation. They cannot do the natural "broad recon → narrow → read whole files" pattern.

**Why:** The user has observed this multiple times and laid out the comparison concretely on 2026-05-19. The mental model is: skills should add *structure* (phase ordering, role specialization, deduping output) but never *subtract* tool freedom. The runtime is already sandboxed — read-only bash is safe.

**How to apply:**
- When adding new tools to `FilesystemToolHost`: add `Bash` (read-only allowlist, block `rm`/`delete`/`remove`/`rmdir`/`unlink`/`mv`/destructive redirects).
- When writing SKILL.md prompts: prefer "you have these tools, explore the codebase to ground your observations" over rigid step-by-step instructions or "emit JSON in exactly this schema". Output schema lock-in is the second-largest source of degraded output (after tool-set narrowness).
- Don't drop observations as "unread file" if the broader claim is sound — only enforce ReadSet for `analyzed_from_source` evidence_mode (which is the existing design, but make sure it doesn't accidentally veto findings with no file anchor that are legitimate schema-only findings).

Linked: [[no-language-specific-parsers]] (same principle — let the LLM do semantic work with real tools instead of pre-digesting it into narrow APIs).
