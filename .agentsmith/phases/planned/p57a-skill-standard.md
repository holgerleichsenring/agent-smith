# Phase 57a: Skill Standard — SKILL.md Format & Provenance

## Goal

Migrate all existing Agent Smith skills from the proprietary YAML format to the
open SKILL.md standard (agentskills.io). Add provenance tracking via `source.md`
and Agent-Smith-specific extensions via `agentsmith.md`. Skills become portable,
auditable, and updatable without losing local customizations.

## Problem

Current skills live as single YAML files (`vuln-analyst.yaml`) with Agent-Smith-
specific fields (`convergence_criteria`, `triggers`). This format is proprietary,
not shareable, and incompatible with the growing ecosystem of community skills.

## New Skill Structure

```
config/skills/
  security/
    vuln-analyst/
      SKILL.md        ← instructions (standard format, external or local)
      agentsmith.md   ← AS-specific extensions (convergence_criteria, notes)
      source.md       ← provenance (optional, only for external skills)
    api-design-auditor/
      SKILL.md        ← local, no source.md needed
      agentsmith.md
    false-positive-filter/
      SKILL.md
      agentsmith.md
  coding/
    ...
  legal/
    ...
```

## SKILL.md Format (Standard)

```markdown
---
name: vuln-analyst
description: >
  Vulnerability analysis for security scans. Use when scanning repositories
  for OWASP Top 10, injection vulnerabilities, authentication issues,
  or any security review task.
allowed-tools: Read
version: 1.0.0
---

# Vulnerability Analyst

You are a security vulnerability analyst...

## What to check
...

## Output format
- severity: CRITICAL | HIGH | MEDIUM | LOW
- confidence: 1-10
- finding: description
- endpoint or file
```

## agentsmith.md Format (AS Extension)

```markdown
# Agent Smith Extensions

## convergence_criteria
- "All endpoints checked for authentication bypass"
- "All findings rated with confidence 1-10"
- "No finding without specific file/endpoint reference"

## notes
Only activated by Triage when auth-related files are in diff.
False positives below confidence 7 are discarded downstream.
```

## source.md Format (Provenance, External Skills Only)

```markdown
# Skill Source

origin: https://github.com/trailofbits/owasp-skills/vuln-analyst
version: v1.2.0
commit: a3f2c1d
reviewed: 2026-04-08
reviewed-by: Holger
notes: >
  Imported without changes to SKILL.md.
  convergence_criteria added in agentsmith.md.
  Triggers verified: match Agent Smith security pipeline use case.
```

## SkillLoader Changes

`SkillLoader` merges all three files into a unified `SkillDefinition`:

```csharp
public sealed record SkillDefinition(
    string Name,
    string Description,        // from SKILL.md frontmatter
    string Instructions,       // from SKILL.md body
    IReadOnlyList<string> ConvergenceCriteria,  // from agentsmith.md
    SkillSource? Source);      // from source.md, null for local skills

public sealed record SkillSource(
    string Origin,
    string Version,
    string Commit,
    DateOnly Reviewed,
    string ReviewedBy);
```

Loading order:
1. Read `SKILL.md` — parse frontmatter + body
2. Read `agentsmith.md` — parse convergence_criteria and notes (optional)
3. Read `source.md` — parse provenance (optional)
4. Merge into `SkillDefinition`

## Migration: Existing Skills

All existing YAML skills are migrated to the new structure. Content stays
identical — only the format changes.

| Old | New |
|-----|-----|
| `vuln-analyst.yaml` | `vuln-analyst/SKILL.md` + `agentsmith.md` |
| `rules:` block | SKILL.md body |
| `convergence_criteria:` | `agentsmith.md` |
| `triggers:` | SKILL.md frontmatter `description:` |
| `display_name:`, `emoji:` | SKILL.md frontmatter |

## Files to Create

- `src/AgentSmith.Contracts/Models/SkillDefinition.cs` — updated record
- `src/AgentSmith.Contracts/Models/SkillSource.cs` — new record
- `src/AgentSmith.Infrastructure/Services/Skills/SkillLoader.cs` — updated loader
- `config/skills/security/vuln-analyst/SKILL.md` + `agentsmith.md`
- `config/skills/security/api-design-auditor/SKILL.md` + `agentsmith.md`
- `config/skills/security/false-positive-filter/SKILL.md` + `agentsmith.md`
- `config/skills/security/auth-reviewer/SKILL.md` + `agentsmith.md`
- `config/skills/security/injection-checker/SKILL.md` + `agentsmith.md`
- `config/skills/security/secrets-detector/SKILL.md` + `agentsmith.md`
- `config/skills/legal/` — all legal skills migrated
- `config/skills/coding/` — all coding skills migrated
- `config/skills/mad/` — all MAD skills migrated

## Files to Modify

- `src/AgentSmith.Infrastructure/Services/Skills/SkillLoader.cs`
- Delete all `config/skills/**/*.yaml` after migration

## Definition of Done

- [ ] All existing skills migrated to SKILL.md format
- [ ] `SkillLoader` reads SKILL.md + agentsmith.md + source.md
- [ ] `convergence_criteria` still works after migration
- [ ] Triage still activates correct skills (description-based matching)
- [ ] All existing tests green
- [ ] No `*.yaml` skill files remaining in `config/skills/`
- [ ] `dotnet build` + `dotnet test` clean

## Dependencies

- None — pure format migration, no logic change
