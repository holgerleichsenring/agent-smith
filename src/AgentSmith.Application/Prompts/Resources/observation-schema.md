## Output Format — SkillObservation

You MUST respond with ONLY a JSON array of observations. No preamble, no markdown fences, no explanation outside the JSON.

Each observation has this shape:

```
{
  "concern": "correctness" | "architecture" | "performance" | "security" | "legal" | "compliance" | "risk",
  "description": "What you observed — the problem or insight",
  "suggestion": "What should be done about it",
  "blocking": true/false,
  "severity": "high" | "medium" | "low" | "info",
  "confidence": 0-100,
  "rationale": "Why you believe this (optional)",
  "location": "File:Line or API path (optional)",
  "effort": "small" | "medium" | "large" (optional)
}
```

**Confidence Calibration:**

| Band | Range | Meaning | Action |
|------|-------|---------|--------|
| Low | 0–30 | Speculative — theoretical risk, no concrete exploit path | Do NOT report. These waste reviewer time. |
| Medium | 31–69 | Plausible — suspicious pattern, but requires specific conditions or further investigation | Report only if you can articulate the specific conditions needed for exploitation. |
| High | 70–100 | Confident — clear vulnerability pattern with known exploitation methods or certain exploit path | Always report. Include concrete code path and attack vector. |

When in doubt, round down. A false positive costs more reviewer time than a missed low-confidence finding.

**`blocking` is for exceptional situations, not for severity.**

`blocking: true` triggers a peer skill to be summoned. It is NOT a severity signal. Use it ONLY when:

- You cannot complete your analysis without input that another skill must produce first
  (e.g. "I need the auth scheme to assess these findings, but it is not in the swagger spec")
- A precondition is missing that, if filled, would change your conclusions
  (e.g. "I cannot rate this header finding because no header baseline is loaded")
- The artefact you were given is corrupt or contradicts itself in a way you cannot resolve alone
  (e.g. "the spec declares OAuth2 but every endpoint uses API keys — confirm with auth-config-reviewer")

Use `blocking: false` for:

- Any finding you can describe and rate yourself, no matter the severity (HIGH SQLi, missing CSP, exposed admin endpoint, …)
- Cases where the right downstream action is "fix it" — not "ask a peer first"
- Suggestions, audit notes, or recommendations

**Rules:**

- Do NOT include an `id` field — IDs are assigned by the framework.
- A HIGH severity finding is `blocking: false` unless your analysis literally cannot continue without peer input.
- If you set `blocking: true`, the `description` MUST state which input you are missing and which peer skill could supply it. "Security concern" alone is not enough.
- `confidence` reflects how certain you are (0 = guess, 100 = certain). See calibration table above.
- Produce 1–5 observations. Prefer fewer, higher-quality observations over many weak ones.

**Examples:**

A routine HIGH-severity finding — NOT blocking, just report it:

```json
[
  {
    "concern": "security",
    "description": "The /api/auth/login endpoint accepts passwords in query parameters, exposing them in server logs and browser history.",
    "suggestion": "Move password to POST body. Update OpenAPI spec and all clients.",
    "blocking": false,
    "severity": "high",
    "confidence": 95,
    "rationale": "OWASP A2:2021 — passwords in URLs are a well-documented vulnerability.",
    "location": "POST /api/auth/login",
    "effort": "small"
  }
]
```

A genuinely blocking case — analysis cannot proceed without peer input:

```json
[
  {
    "concern": "security",
    "description": "Cannot evaluate header findings: no api-headers baseline is loaded. security-headers-auditor needs the baseline catalog file before it can decide which headers are missing vs. intentionally absent.",
    "suggestion": "Load baselines/api-headers.yaml from the skill catalog or supply a project override.",
    "blocking": true,
    "severity": "info",
    "confidence": 100,
    "location": "baseline:api-headers"
  }
]
```
