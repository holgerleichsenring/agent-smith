# Security scan detection floor

> A PUBLIC CORPUS CANNOT GRADE THIS SCAN. Every well-known weakness is in the training data and a planted defect is formulaic in a way a real one is not, so a green floor proves only that the scan is wired, reaches the code and emits findings at all. It is not a quality score and must never be quoted as one.

- model: `sonnet`
- scan master: `7e91dde7`
- generated: 2026-09-01T21:50:10.6623440+00:00

**Misses:** 0/5 (0 %) — declared weaknesses no delivered finding named.

**False alarms:** 1/5 (20 %) — sound files a finding named anyway.

Cited line matched on 4 of 5 detections — a citation sub-metric, not a gate.

**Contributed nothing to this score:** DependencyAudit (did not run), GitHistoryScan (found nothing) — a score is not a complete measurement of a scan whose steps stayed silent.

## reference-service
- [x] src/orders/orderLookup.ts (sql-injection, flawed)
  - found [High]: src/orders/orderLookup.ts:7: SQL built by concatenating req.params.id — classic SQL injection in the findOrder handler (on the declared line)
- [x] src/admin/memberAdmin.ts (missing-authorization, flawed)
  - found [High]: src/admin/memberAdmin.ts:17: removeMember is missing the requireAdmin() guard that listMembers and suspendMember both enforce — any authenticated caller can delete any member (on the declared line)
- [x] src/files/attachmentDownload.ts (path-traversal, flawed)
  - found [High]: src/files/attachmentDownload.ts:9: path traversal — user-supplied query.name is joined to STORAGE_ROOT with no containment check, allowing reads of arbitrary files on the host (on the declared line)
- [x] src/auth/loginRedirect.ts (open-redirect, flawed)
  - found [Medium]: src/auth/loginRedirect.ts:6-7: open redirect — req.query.next is forwarded to res.redirect() without validation against an allowlist, enabling phishing via crafted login URLs
- [x] src/jobs/jobPayload.ts (unsafe-deserialization, flawed)
  - found [Critical]: src/jobs/jobPayload.ts:7: eval() called on raw job payload text — arbitrary code execution if a producer (or anyone who can write job records) controls payloadText (on the declared line)
- [FALSE ALARM] src/reports/reportLookup.ts (sql-injection, clean)
  - found [Info]: src/reports/reportLookup.ts:15: scanner SQL-template-literal hit is a FALSE POSITIVE — table name and ORDER BY column are resolved from a closed const whitelist before interpolation; only memberId and limit reach the database as bound parameters (on the declared line)
- [x] src/files/exportPath.ts (path-traversal, clean)
- [x] src/auth/returnTarget.ts (open-redirect, clean)
- [x] .agentsmith/contexts/default/context.yaml (project-metadata, clean)
- [x] .agentsmith/contexts/default/principles.md (project-metadata, clean)
