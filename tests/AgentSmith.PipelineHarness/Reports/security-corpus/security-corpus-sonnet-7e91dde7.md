# Security scan detection floor

> A PUBLIC CORPUS CANNOT GRADE THIS SCAN. Every well-known weakness is in the training data and a planted defect is formulaic in a way a real one is not, so a green floor proves only that the scan is wired, reaches the code and emits findings at all. It is not a quality score and must never be quoted as one.

- model: `sonnet`
- scan master: `7e91dde7`
- generated: 2026-09-23T22:53:43.2494580+00:00

**Misses:** 0/5 (0 %) — declared weaknesses no delivered finding named.

**False alarms:** 0/5 (0 %) — sound files a finding named anyway.

Cited line matched on 3 of 5 detections — a citation sub-metric, not a gate.

**Contributed nothing to this score:** DependencyAudit (did not run), GitHistoryScan (found nothing) — a score is not a complete measurement of a scan whose steps stayed silent.

## reference-service
- [x] src/orders/orderLookup.ts (sql-injection, flawed)
  - found [High]: src/orders/orderLookup.ts:7: SQL built by string concatenation of req.params.id — "SELECT … WHERE id = '" + orderId + "'" is a textbook SQL injection sink. (on the declared line)
- [x] src/admin/memberAdmin.ts (missing-authorization, flawed)
  - found [Critical]: src/admin/memberAdmin.ts:16: removeMember performs no authorization check — requireAdmin is called in listMembers (line 6) and suspendMember (line 11) but is absent from removeMember, letting any caller delete any member.
- [x] src/files/attachmentDownload.ts (path-traversal, flawed)
  - found [High]: src/files/attachmentDownload.ts:9: Path traversal — join(STORAGE_ROOT, name) is called with caller-supplied name without containment check, allowing reads of arbitrary server files via ../../ sequences. (on the declared line)
- [x] src/auth/loginRedirect.ts (open-redirect, flawed)
  - found [High]: src/auth/loginRedirect.ts:6: Open redirect — next parameter is taken verbatim from the query string and passed to res.redirect() with no allowlist validation, enabling phishing redirects after login.
- [x] src/jobs/jobPayload.ts (unsafe-deserialization, flawed)
  - found [Critical]: src/jobs/jobPayload.ts:7: eval() executes raw job payload text — eval('(' + raw + ')') runs arbitrary JavaScript from the job record, enabling remote code execution if any queue producer is compromised or attacker-controlled. (on the declared line)
- [x] src/reports/reportLookup.ts (sql-injection, clean)
- [x] src/files/exportPath.ts (path-traversal, clean)
- [x] src/auth/returnTarget.ts (open-redirect, clean)
- [x] .agentsmith/contexts/default/context.yaml (project-metadata, clean)
- [x] .agentsmith/contexts/default/principles.md (project-metadata, clean)
