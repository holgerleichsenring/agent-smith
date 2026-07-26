---
name: feedback_no_language_specific_parsers
description: "Don't write per-language regex parsers OR per-framework cheat sheets in prompts; trust the LLM's training"
metadata:
  type: feedback
status: proposed
---
Don't build per-language/per-framework regex parsers (route mapping, auth detection, upload site extraction, etc.) in agent-smith. Don't hardcode framework lists or closed enums for things the LLM understands natively. Use the LLM.

**Why:** agent-smith is fundamentally an LLM-driven tool. The LLM already knows ASP.NET `[Route("api/[controller]")]`, Spring `@RequestMapping`, FastAPI routers, Express middleware, NestJS guards, Kotlin Ktor, etc. from training. Three failure modes that keep this anti-pattern coming back:

1. **Per-language regex extractors** (`RouteMapper`, `AuthBootstrapExtractor`, `UploadHandlerExtractor`, `DotNetRouteExtractor`): lock the codebase to one framework per parser, drift as frameworks evolve, duplicate LLM training.
2. **Framework cheat-sheets in skill prompts** ("ASP.NET uses [Route], Spring uses @RequestMapping, ..."): regex thinking in prose form. Goes stale, can't cover frameworks I don't list, condescends to the LLM. The operator explicitly called this out as the same anti-pattern as the parsers.
3. **Closed enums for language/framework** (e.g. `project_language: csharp | node | python | generic`): the "generic" bucket is the giveaway — it's a fallback for everything the closed set excludes. Pass the actual detected language as a free-form string and trust the LLM.

Concrete incidents that produced this principle: (a) `DotNetRouteExtractor` PR #134 closed immediately — wrong layer; (b) my next attempt at a skill prompt with "ASP.NET uses [Route]/[Http*], Spring uses @RequestMapping, Express uses router.post()" — same anti-pattern in prompt form.

**How to apply:** Reach for an LLM call (a skill, or an LLM invocation from a pipeline step with `IChatClientFactory`) when the task is "understand code semantics across frameworks/languages": route binding, framework-specific decorators, auth flow recognition, upload handler discovery, ORM relationship extraction, comment-intent extraction beyond pure slash-command structure, etc. Write skill prompts that name the goal, not the syntax: "Find the API implementation for endpoint X in this project. Project language is {project_language}." — no framework list, no syntax cheat-sheet.

Trivial structural extraction is still fine for regex — anything where a different framework would not require a different parser. Slash-command discrimination (`/approve` vs `/agent-smith ...`) is structural; intent extraction past the slash is semantic.

Two reflexes that cause this to creep back in: (a) type-safety anxiety (enum + regex feel predictable, LLM output feels wobbly — answer: trust + JSON schema + verification); (b) cost anxiety (LLM call has latency/cost — answer: pay where intelligence helps, don't fake-substitute with regex).
