---
name: feedback_no_customer_names
description: "Never write customer, project, or target identifiers into any artifact that lands in a public repo — anonymize from the first draft"
metadata:
  type: feedback
status: proposed
---
Never write customer, project, or target identifiers — neither in clean form nor in meta-form ("the <X> prefix", "removed <Y>.* references") — into ANY artifact that lands in a public repository: phase specs, skill catalogs, decision docs, READMEs, baseline snapshots, **commit messages and their bodies**, PR bodies, test fixtures, deploy/compose volume paths, local home-directory paths.

**Why:** These repositories are public. Reciting a customer or internal project name — even as an example, or while explaining what was removed — leaks internal context into a public history and stays grep-able forever. Retroactive removal is expensive: a full git-filter-repo history rewrite + force-push has been necessary more than once, and one pass was triggered SPECIFICALLY by a scrub commit whose body listed the offending strings while announcing their removal. Even after a rewrite, external mirrors, snapshots, and forks retain copies.

**How to apply:**
- Anonymize from the FIRST draft, never as a later cleanup pass: "Sample", "the reference target", "the target API", "the operator's production deployment".
- For internal file paths: drop the identifying prefix (`<api>/Middleware/AuthenticationMiddleware.cs`) or omit the path entirely.
- For findings recited from a real run: genericize the role ("a permission cache service"), never the real type name.
- Public library names that identify no customer (EF Core, ASP.NET Core) are fine.
- **Meta-form rule:** when discussing a scrub in commits/PR bodies/spec text, NEVER quote the strings being removed — "the customer fingerprint pattern", never the literal. A commit body that says "removed <X> mentions" leaks the very thing it cleans.
- A pre-commit fingerprint gate exists — treat a hit as a hard stop, never rephrase around it.

Related: [[feedback_specs_english_only]] (same open-source-hygiene reasoning).
