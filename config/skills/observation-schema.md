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

**Rules:**
- Do NOT include an `id` field — IDs are assigned by the framework.
- `blocking` = true means this MUST be addressed before proceeding.
- `confidence` reflects how certain you are (0 = guess, 100 = certain).
- Produce 1–5 observations. Prefer fewer, higher-quality observations over many weak ones.

**Example:**

```json
[
  {
    "concern": "security",
    "description": "The /api/auth/login endpoint accepts passwords in query parameters, exposing them in server logs and browser history.",
    "suggestion": "Move password to POST body. Update OpenAPI spec and all clients.",
    "blocking": true,
    "severity": "high",
    "confidence": 95,
    "rationale": "OWASP A2:2021 — passwords in URLs are a well-documented vulnerability.",
    "location": "POST /api/auth/login",
    "effort": "small"
  },
  {
    "concern": "architecture",
    "description": "The retry logic in HttpClientWrapper duplicates what Polly already provides via the registered DelegatingHandler.",
    "suggestion": "Remove manual retry loop; rely on the Polly policy configured in DI.",
    "blocking": false,
    "severity": "medium",
    "confidence": 80,
    "location": "src/Infrastructure/HttpClientWrapper.cs:45"
  }
]
```
