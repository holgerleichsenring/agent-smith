# Phase 57b: Skill Manager Pipeline

## Goal

A new pipeline type `skill-manager` that autonomously finds, reviews, installs,
and updates skills. Agent Smith manages its own skills — discovers external skills,
reviews them for quality and safety, places them locally with provenance tracking,
and waits for human approval before activating.

## Problem

Today skills are written manually. There is no way to discover external skills,
evaluate them against Agent Smith's use cases, or update them when better versions
appear. The skill library grows only through human effort.

## Pipeline: skill-manager

```
Trigger: cli, slack, scheduled
  "find better skills for security-scan"
  "update outdated skills"
  "review skill vuln-analyst"
  "add skill from github:trailofbits/owasp-skills"
```

### Steps

```
DiscoverSkills         ← web search + known registries
EvaluateSkills         ← review content, fit, safety
DraftSkillFiles        ← write SKILL.md + agentsmith.md + source.md
ApproveSkills          ← WaitForHuman: show diff, require explicit OK
InstallSkills          ← place files in config/skills/
WriteRunResult
```

### DiscoverSkillsHandler

Searches for skills relevant to a given pipeline or topic:

Sources (in order):
1. `agentskills.io` registry
2. `github:anthropics/skills`
3. `github:awesome-copilot` skill collection
4. `github:alirezarezvani/claude-skills`
5. Web search for topic-specific skills

For each candidate skill, extracts:
- Name, description, source URL
- Content of SKILL.md
- Version/commit

### EvaluateSkillsHandler

For each discovered skill, I evaluate:

**Fit assessment:**
- Does the description match the target pipeline?
- Does the instruction content do what Agent Smith needs?
- Is there overlap with existing skills?
- Are triggers precise or too broad?

**Safety assessment:**
- Any prompt injection patterns?
- Does it request tools beyond what it needs?
- Does it try to exfiltrate data?
- Is the source trustworthy?

Output: ranked list with fit score (1-10), safety score (1-10), recommendation.

### DraftSkillFilesHandler

For approved candidates, writes:
- `SKILL.md` — verbatim from source (never modified)
- `agentsmith.md` — convergence_criteria derived from skill content
- `source.md` — provenance with today's date and my review summary

### ApproveSkillsHandler

Sends to Slack/Teams:

```
New skill found: trailofbits/differential-review
Fit: 8/10 — matches security-scan diff review use case
Safety: 9/10 — read-only tools, no external calls
Source: github:trailofbits/owasp-skills v1.2.0

SKILL.md preview:
[first 20 lines]

[✅ Install]  [❌ Skip]  [🔍 Full review]
```

Human must explicitly approve. No skill is installed without confirmation.

### Configuration

```yaml
projects:
  agent-smith-skills:
    pipeline: skill-manager
    skills_target: config/skills/security   # where to install
    trigger:
      schedule: "0 9 * * 1"   # every Monday 9am
      slack: true
    search:
      topics: [security, api-scan, owasp]
      min_fit_score: 7
      min_safety_score: 8
```

## Key Design Decisions

1. **SKILL.md is never modified** — external content stays verbatim.
   Agent Smith extensions go in `agentsmith.md` only.

2. **Human approval is mandatory** — no skill is installed without explicit OK.
   This is not optional. Skills are prompts injected into the agent's context.

3. **Pinned versions** — `source.md` always records the exact commit.
   Updates require a new review cycle, not automatic pull.

4. **I do the review** — the evaluation is an agentic step, not a script.
   I read the skill content and assess fit and safety in natural language.
   The human sees my assessment before deciding.

## Files to Create

- `src/AgentSmith.Contracts/Commands/CommandNames.cs` — add DiscoverSkills,
  EvaluateSkills, DraftSkillFiles, InstallSkills
- `src/AgentSmith.Application/Services/Handlers/DiscoverSkillsHandler.cs`
- `src/AgentSmith.Application/Services/Handlers/EvaluateSkillsHandler.cs`
- `src/AgentSmith.Application/Services/Handlers/DraftSkillFilesHandler.cs`
- `src/AgentSmith.Application/Services/Handlers/InstallSkillsHandler.cs`
- `src/AgentSmith.Contracts/Commands/PipelinePresets.cs` — add SkillManager
- `src/AgentSmith.Contracts/Models/SkillCandidate.cs`
- `src/AgentSmith.Contracts/Models/SkillEvaluation.cs`

## Definition of Done

- [ ] `skill-manager` pipeline preset resolves
- [ ] `DiscoverSkillsHandler` searches at least 3 sources
- [ ] `EvaluateSkillsHandler` produces fit + safety scores
- [ ] `DraftSkillFilesHandler` writes all three files correctly
- [ ] `ApproveSkillsHandler` sends Slack message with preview + buttons
- [ ] `InstallSkillsHandler` places files in correct directory
- [ ] No skill installed without human approval
- [ ] `source.md` written for every externally sourced skill
- [ ] Schedule trigger works (cron-based)
- [ ] All existing tests green
- [ ] `dotnet build` + `dotnet test` clean

## Dependencies

- p57a (SKILL.md format — skills must be in new format before installing)
- p58 (Interactive Dialogue — ApproveSkills uses WaitForHuman)
