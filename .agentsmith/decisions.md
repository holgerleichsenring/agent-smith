# Decision Log

## p66: Docs Enhancement — Self-Documentation & Multi-Agent Orchestration
- [Architecture] DESIGN.md placed in docs/ not project root — it is a docs-site concern, not product code
- [Tooling] CSS-only theme overrides via extra_css, no custom MkDocs templates — keeps MkDocs upgrades safe
- [TradeOff] Content first, styling second — missing content is a blocker, imperfect styling is not
- [Implementation] Reuse existing fix-and-feature.md instead of creating separate fix-bug.md — page already covers both pipelines

## p67: API Scan Compression & ZAP Fix
- [Architecture] Category slicing (auth/design/runtime) instead of finding compression — findings are already compact at ~90 chars/piece, compression would lose information. Slicing routes findings to the right skill without data loss.
- [Tooling] WorkDir as optional ToolRunRequest parameter instead of Docker volume mounts — volume mounts would add complexity to DockerToolRunner. WorkDir + tar extraction to / is simpler and backward compatible (Nuclei/Spectral unaffected).
- [Implementation] Inject target URL into swagger servers[] instead of pinning ZAP version — ZAP needs absolute URLs, many OpenAPI specs only have relative "/". Patching the spec before copy is non-invasive.
- [TradeOff] Remove --auto flag entirely instead of finding replacement — --auto was never a valid option on ZAP's Python wrapper scripts. The scripts are non-interactive by default in Docker containers.
- [Implementation] Skip DAST skills on ZAP failure via ZapFailed flag — avoids wasting 2 LLM calls on empty input. Flag is checked in ApiSecurityTriageHandler before building the skill graph.

## p68: API Finding Location
- [Architecture] DisplayLocation as computed property on Finding record — no new field in serialization, just display logic. Fallback chain: ApiPath > SchemaName > File:StartLine.
- [TradeOff] Nullable fields instead of separate ApiFinding subtype — keeps one Finding type across all pipelines. Security-scan findings simply leave ApiPath/SchemaName null.
- [Implementation] NullIfEmpty normalization in ParseGateFindings — LLMs return empty strings instead of omitting fields. Normalize at parse time and defend in DisplayLocation with IsNullOrWhiteSpace.
