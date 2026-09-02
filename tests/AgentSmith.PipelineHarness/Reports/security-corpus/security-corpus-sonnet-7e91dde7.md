# Security scan detection floor

> A PUBLIC CORPUS CANNOT GRADE THIS SCAN. Every well-known weakness is in the training data and a planted defect is formulaic in a way a real one is not, so a green floor proves only that the scan is wired, reaches the code and emits findings at all. It is not a quality score and must never be quoted as one.

- model: `sonnet`
- scan master: `7e91dde7`
- generated: 2026-09-02T05:18:42.8575210+00:00

**Misses:** 0/5 (0 %) — declared weaknesses no delivered finding named.

**False alarms:** 0/5 (0 %) — sound files a finding named anyway.

Cited line matched on 3 of 5 detections — a citation sub-metric, not a gate.

**Contributed nothing to this score:** DependencyAudit (did not run), GitHistoryScan (found nothing) — a score is not a complete measurement of a scan whose steps stayed silent.

## reference-service
- [x] src/orders/orderLookup.ts (sql-injection, flawed)
  - found [High]: src/orders/orderLookup.ts:7: SQL built by string-concatenating req.params.id — classic SQL injection (on the declared line)
- [x] src/admin/memberAdmin.ts (missing-authorization, flawed)
  - found [High]: src/admin/memberAdmin.ts:16: removeMember lacks requireAdmin check — any caller can delete arbitrary members
- [x] src/files/attachmentDownload.ts (path-traversal, flawed)
  - found [High]: src/files/attachmentDownload.ts:9: path traversal — req.query.name joined to STORAGE_ROOT with no containment check (on the declared line)
- [x] src/auth/loginRedirect.ts (open-redirect, flawed)
  - found [High]: src/auth/loginRedirect.ts:6: open redirect — req.query.next forwarded to res.redirect() without allowlist validation
- [x] src/jobs/jobPayload.ts (unsafe-deserialization, flawed)
  - found [Critical]: src/jobs/jobPayload.ts:7: job payload deserialized via eval() — arbitrary code execution if queue data is attacker-influenced (on the declared line)
- [x] src/reports/reportLookup.ts (sql-injection, clean)
- [x] src/files/exportPath.ts (path-traversal, clean)
- [x] src/auth/returnTarget.ts (open-redirect, clean)
- [x] .agentsmith/contexts/default/context.yaml (project-metadata, clean)
- [x] .agentsmith/contexts/default/principles.md (project-metadata, clean)
