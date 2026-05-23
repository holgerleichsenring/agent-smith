# Skill Manager

The **skill-manager** pipeline autonomously discovers, evaluates, and installs skills. Agent Smith manages its own skill library -- finding external skills, reviewing them for quality and safety, and waiting for human approval before activation.

## Pipeline Steps

| # | Command | What It Does |
|---|---------|--------------|
| 1 | DiscoverSkills | Searches registries and web for relevant skills |
| 2 | EvaluateSkills | Reviews content, fit, and safety of each candidate |
| 3 | DraftSkillFiles | Writes SKILL.md + agentsmith.md + source.md for approved candidates |
| 4 | ApproveSkills | Sends preview to Slack/CLI, requires explicit human approval |
| 5 | InstallSkills | Places approved skill files in `config/skills/` |
| 6 | WriteRunResult | Logs the skill management run |

## Discovery Sources

The `DiscoverSkills` step searches for skills relevant to a given pipeline or topic:

1. `agentskills.io` registry
2. `github:anthropics/skills`
3. `github:awesome-copilot` skill collection
4. `github:alirezarezvani/claude-skills`
5. Web search for topic-specific skills

For each candidate, the handler extracts the skill name, description, source URL, SKILL.md content, and version/commit.

## Evaluation

Each discovered skill is evaluated on two axes:

**Fit assessment (1-10):**

- Does the description match the target pipeline?
- Does the instruction content do what Agent Smith needs?
- Is there overlap with existing skills?

**Safety assessment (1-10):**

- Any prompt injection patterns?
- Does it request tools beyond what it needs?
- Does it try to exfiltrate data?
- Is the source trustworthy?

The evaluation is an agentic step -- the LLM reads the skill content and assesses fit and safety in natural language. The human sees the assessment before deciding.

## Approval

No skill is installed without explicit human approval. The approval message includes:

```
New skill found: trailofbits/differential-review
Fit: 8/10 -- matches security-scan diff review use case
Safety: 9/10 -- read-only tools, no external calls
Source: github:trailofbits/owasp-skills v1.2.0

SKILL.md preview:
[first 20 lines]

[Install]  [Skip]  [Full review]
```

## SKILL.md Format

Skills use the open SKILL.md standard with Agent Smith extensions:

```
config/skills/security/vuln-analyst/
  SKILL.md         # Instructions (standard format, never modified)
  agentsmith.md    # AS-specific: convergence_criteria, notes
  source.md        # Provenance (only for external skills)
```

- **SKILL.md** -- the skill instructions, kept verbatim from the source
- **agentsmith.md** -- Agent Smith-specific extensions like convergence criteria
- **source.md** -- provenance tracking: origin URL, version, commit, review date, reviewer

See [Skills Reference](../configuration/skills.md) for the complete format specification.

## Configuration

```yaml
projects:
  agent-smith-skills:
    pipeline: skill-manager
    skills_target: config/skills/security
    trigger:
      schedule: "0 9 * * 1"       # every Monday 9am
      slack: true
    search:
      topics: [security, api-scan, owasp]
      min_fit_score: 7
      min_safety_score: 8
```

## CLI

```bash
# Discover and evaluate skills for a topic
agent-smith skill-manager --project my-skills --topic security

# Add a specific skill from a known source
agent-smith skill-manager --project my-skills --add github:trailofbits/owasp-skills

# Review and update outdated skills
agent-smith skill-manager --project my-skills --update
```

## Key Design Decisions

1. **SKILL.md is never modified** -- external content stays verbatim. Agent Smith extensions go in `agentsmith.md` only.
2. **Human approval is mandatory** -- skills are prompts injected into the agent's context. No skill is installed without explicit confirmation.
3. **Pinned versions** -- `source.md` always records the exact commit. Updates require a new review cycle, not automatic pull.
