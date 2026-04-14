# Phase 73: Class Size Enforcement

## Goal

Bring all production source files under the 120-line limit defined in
`context.yaml` (quality.limits.class-lines). Add CI enforcement so the
limit cannot silently drift again.

---

## Current State

78 production files exceed 120 lines (as of 2026-04-14, 873 tests).

| Tier | Lines | Files | Action |
|------|-------|-------|--------|
| Critical | >300 | 10 | Split into focused classes |
| High | 200–300 | 22 | Extract helpers / split responsibilities |
| Low | 121–200 | 46 | Inline cleanup, extract where obvious |

---

## Tier 1: Critical (>300 lines) — must split

| Lines | File | Split Strategy |
|-------|------|----------------|
| 538 | `Server/Services/Adapters/SlackAdapter.cs` | Extract event handlers → SlackEventRouter, SlackCommandHandler |
| 525 | `Server/Extensions/WebApplicationExtensions.cs` | Extract per-feature endpoint groups into separate extension methods/files |
| 491 | `Infrastructure/Services/Security/DependencyAuditor.cs` | Extract parsers per ecosystem (NuGet, npm, pip) |
| 411 | `Infrastructure.Core/Services/YamlSkillLoader.cs` | Extract SKILL.md parser, validation, migration logic |
| 390 | `Application/Services/Handlers/ApiSkillRoundHandler.cs` | Extract prompt builder, response parser |
| 384 | `Application/Services/Handlers/SkillRoundHandlerBase.cs` | Extract convergence logic, round result builder |
| 321 | `Server/Services/Adapters/TeamsCardBuilder.cs` | Extract card templates into per-type builders |
| 315 | `Server/Services/MessageBusListener.cs` | Extract message routing, job lifecycle |
| 313 | `Cli/Services/WebhookListener.cs` | Extract platform detection, signature validation (already partially done in p72) |
| 312 | `Server/Services/Adapters/TeamsAdapter.cs` | Extract message formatter, interaction handler |

## Tier 2: High (200–300 lines) — extract where clear

22 files. Common patterns:
- **Handlers** with inline prompt building → extract prompt builder
- **Providers** with multiple API operations → extract per-operation methods into partial classes or helpers
- **Compressors** (SecurityFindingsCompressor, ApiScanFindingsCompressor) → extract category slicing
- **DI extensions** (ServiceCollectionExtensions) → split by layer/concern

## Tier 3: Low (121–200 lines) — case-by-case

46 files. Many are close to the limit and can be brought under with:
- Extracting large string literals / constants
- Moving nested helper methods to standalone classes
- Removing dead code

---

## Step 1: CI Gate

Add a script that fails the build when any `.cs` file in `src/` exceeds
120 lines. This prevents new violations while the cleanup is in progress.

**File:** `scripts/check-class-size.sh`

```bash
#!/usr/bin/env bash
set -euo pipefail
LIMIT=120
VIOLATIONS=$(find src -name "*.cs" -exec awk -v limit="$LIMIT" \
  'END { if (NR > limit) print NR "\t" FILENAME }' {} \; | sort -rn)
if [ -n "$VIOLATIONS" ]; then
  echo "Files exceeding $LIMIT lines:"
  echo "$VIOLATIONS"
  echo ""
  echo "$(echo "$VIOLATIONS" | wc -l | tr -d ' ') file(s) exceed the limit."
  # During cleanup phase: warn only. Change to exit 1 when Tier 1+2 are done.
  # exit 1
fi
```

Add to CI workflow as a non-blocking step initially, switch to blocking
after Tier 1 and Tier 2 are cleaned up.

---

## Step 2: Tier 1 Cleanup (10 files)

Split each file using the strategies listed above. Each split must:
- Preserve all existing tests (no test changes unless class was renamed)
- Keep the public API surface identical (callers unchanged)
- Use `internal` visibility for extracted helpers where possible

---

## Step 3: Tier 2 Cleanup (22 files)

Same approach. Batch by project to minimize cross-cutting changes:
- `Application/` handlers: 8 files
- `Infrastructure/` providers + tools: 8 files
- `Server/` adapters + services: 4 files
- `Infrastructure.Core/`: 2 files

---

## Step 4: Tier 3 Cleanup (46 files)

Lightweight pass. Many of these will naturally fall under 120 during
Tier 1+2 work (shared base classes shrink when extracted). Re-measure
after Tier 1+2 before starting Tier 3.

---

## Step 5: Enforce

Flip the CI gate to blocking (`exit 1`). From this point forward, no
PR can merge with a file exceeding 120 lines.

---

## Coding Principles (apply throughout)

- **SOLID** — SRP drives the splits. ISP/DIP: extract interfaces where
  a class gains a new collaborator. One reason to change per class.
- **DRY** — if splitting reveals duplication, extract it.
- **Tell Don't Ask** — extracted classes own their data and behavior.
  Callers tell, don't reach in.
- **One type per file** — every new class, interface, enum, record gets
  its own file. No exceptions.
- **No optional parameters** — use overloads or builder patterns.
- **Clean abstractions with interfaces** — new interfaces are expected
  and encouraged when extracting responsibilities. Register in DI.
- **Preserve conventions** — file-scoped namespaces, sealed default,
  primary constructors, records for DTOs.

## Constraints

- **No behavior changes.** This is pure refactoring — extract, rename, split.
- **Tests may change** when classes are renamed or split, but test coverage
  must not decrease.

---

## Definition of Done

- [ ] All `src/` files ≤ 120 lines
- [ ] CI gate blocks files > 120 lines
- [ ] One type per file — no multi-type files
- [ ] New abstractions have interfaces + DI registration
- [ ] No optional parameters in new/changed signatures
- [ ] `dotnet build` + `dotnet test` clean
