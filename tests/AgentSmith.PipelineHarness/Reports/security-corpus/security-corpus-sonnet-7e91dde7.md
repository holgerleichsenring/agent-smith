# Security scan detection floor

> A PUBLIC CORPUS CANNOT GRADE THIS SCAN. Every well-known weakness is in the training data and a planted defect is formulaic in a way a real one is not, so a green floor proves only that the scan is wired, reaches the code and emits findings at all. It is not a quality score and must never be quoted as one.

- model: `sonnet`
- scan master: `7e91dde7`
- generated: 2026-10-02T21:44:53.2111160+00:00

**Misses:** 0/5 (0 %) — declared weaknesses no delivered finding named.

**False alarms:** 1/5 (20 %) — sound files a finding named anyway.

Cited line matched on 2 of 5 detections — a citation sub-metric, not a gate.

**Contributed nothing to this score:** DependencyAudit (did not run), GitHistoryScan (found nothing) — a score is not a complete measurement of a scan whose steps stayed silent.

## reference-service
- [x] src/orders/orderLookup.ts (sql-injection, flawed)
  - found [Critical]: src/orders/orderLookup.ts:7: findOrder builds SQL by string-concatenating req.params.id directly into the query, enabling SQL injection. (on the declared line)
- [x] src/admin/memberAdmin.ts (missing-authorization, flawed)
  - found [Critical]: src/admin/memberAdmin.ts:16-18: removeMember has no requireAdmin() (or any authorization) check, unlike listMembers and suspendMember in the same file, letting any caller delete a member record.
- [x] src/files/attachmentDownload.ts (path-traversal, flawed)
  - found [High]: src/files/attachmentDownload.ts:8-10: downloadAttachment joins unvalidated req.query.name onto STORAGE_ROOT with no resolve/containment check, allowing path traversal to read arbitrary files.
- [x] src/auth/loginRedirect.ts (open-redirect, flawed)
  - found [Medium]: src/auth/loginRedirect.ts:6-7: finishLogin redirects to req.query.next without validating it against an allowlist, enabling an open redirect after login.
- [x] src/jobs/jobPayload.ts (unsafe-deserialization, flawed)
  - found [High]: src/jobs/jobPayload.ts:7: readPayload uses eval("(" + raw + ")") to parse a job's payload text instead of JSON.parse, allowing arbitrary code execution if the payload text is ever attacker- or producer-compromised. (on the declared line)
- [FALSE ALARM] src/reports/reportLookup.ts (sql-injection, clean)
  - found [Medium]: src/reports/reportLookup.ts:15: SQL built with a template literal interpolating table/orderBy, flagged by the scanner as Critical, but both values are drawn only from the hardcoded REPORTABLE allowlist after a guard check — not exploitable. (on the declared line)
- [x] src/files/exportPath.ts (path-traversal, clean)
- [x] src/auth/returnTarget.ts (open-redirect, clean)
- [x] .agentsmith/contexts/default/context.yaml (project-metadata, clean)
- [x] .agentsmith/contexts/default/principles.md (project-metadata, clean)
