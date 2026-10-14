# Security scan detection floor

> A PUBLIC CORPUS CANNOT GRADE THIS SCAN. Every well-known weakness is in the training data and a planted defect is formulaic in a way a real one is not, so a green floor proves only that the scan is wired, reaches the code and emits findings at all. It is not a quality score and must never be quoted as one.

- model: `sonnet`
- scan master: `7e91dde7`
- generated: 2026-10-14T08:35:54.6833260+00:00

**Misses:** 0/5 (0 %) — declared weaknesses no delivered finding named.

**False alarms:** 0/5 (0 %) — sound files a finding named anyway.

Cited line matched on 4 of 5 detections — a citation sub-metric, not a gate.

**Contributed nothing to this score:** DependencyAudit (did not run), GitHistoryScan (found nothing) — a score is not a complete measurement of a scan whose steps stayed silent.

## reference-service
- [x] src/orders/orderLookup.ts (sql-injection, flawed)
  - found [Critical]: src/orders/orderLookup.ts:7: findOrder builds SQL by concatenating req.params.id into the query string ("... WHERE id = '" + orderId + "'"), which allows SQL injection. The query also has no member_id filter, so any caller can read any member's order. (on the declared line)
- [x] src/admin/memberAdmin.ts (missing-authorization, flawed)
  - found [High]: src/admin/memberAdmin.ts:16: removeMember deletes a member via members.remove(req.params.id) without calling requireAdmin(req). The sibling handlers listMembers and suspendMember both call it, so any caller who can reach this route can delete any member.
- [x] src/files/attachmentDownload.ts (path-traversal, flawed)
  - found [High]: src/files/attachmentDownload.ts:9: downloadAttachment passes the user-supplied req.query.name to join(STORAGE_ROOT, name) and then readFile. The path is never resolved or checked against the root, so "../../etc/passwd" reads arbitrary files (path traversal). (on the declared line)
- [x] src/auth/loginRedirect.ts (open-redirect, flawed)
  - found [Medium]: src/auth/loginRedirect.ts:7: finishLogin redirects to String(req.query.next) without validation. This is an open redirect after login, usable for phishing with a link on a trusted domain. (on the declared line)
- [x] src/jobs/jobPayload.ts (unsafe-deserialization, flawed)
  - found [Critical]: src/jobs/jobPayload.ts:7: readPayload runs eval("(" + raw + ")") on job.payloadText, a payload queued by a producer. Anyone who can influence a queued payload gets arbitrary code execution in the worker process. (on the declared line)
- [x] src/reports/reportLookup.ts (sql-injection, clean)
- [x] src/files/exportPath.ts (path-traversal, clean)
- [x] src/auth/returnTarget.ts (open-redirect, clean)
- [x] .agentsmith/contexts/default/context.yaml (project-metadata, clean)
- [x] .agentsmith/contexts/default/principles.md (project-metadata, clean)
