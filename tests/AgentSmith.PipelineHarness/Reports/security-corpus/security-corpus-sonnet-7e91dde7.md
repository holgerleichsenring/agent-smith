# Security scan detection floor

> A PUBLIC CORPUS CANNOT GRADE THIS SCAN. Every well-known weakness is in the training data and a planted defect is formulaic in a way a real one is not, so a green floor proves only that the scan is wired, reaches the code and emits findings at all. It is not a quality score and must never be quoted as one.

- model: `sonnet`
- scan master: `7e91dde7`
- generated: 2026-09-13T20:06:36.8876280+00:00

**Misses:** 0/0 (0 %) — declared weaknesses no delivered finding named.

**False alarms:** 0/0 (0 %) — sound files a finding named anyway.

Cited line matched on 0 of 0 detections — a citation sub-metric, not a gate.

**Contributed nothing to this score:** DependencyAudit (did not run), GitHistoryScan (found nothing), StaticPatternScan (loaded no pattern definitions — it did not look) — a score is not a complete measurement of a scan whose steps stayed silent.

## reference-service
- SCAN NOT TAKEN: Prompt 'project-analyzer-system' must come from the skill catalog's 'project-analyzer-master' master, but the loaded catalog does not provide it. Pin a skills.version that includes it (the embedded fallback was removed in p0205). Point agentsmith.yml's skills source at a directory/version that has it.
