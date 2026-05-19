# p0151: evidence-grounded skill pipelines (umbrella decision record)

Summary of the umbrella as it actually shipped — eight PRs, one cross-repo PR, one parked sub-phase.

## What the umbrella delivered

| Sub-phase | PR | What it does |
|-----------|-----|-------------|
| p0151a | #160 | LoopTraceCollector wired via TracingChatClient + TracingAIFunction decorators; RunCommand enabled in Plan/Survey tool surfaces; SourceAnchoringPreamble prepended to every skill prompt by PromptComposer. |
| p0151b | #161 | EvidenceMode JSON unified on snake_case; ReadSet-anchored validator (`SourceAnchorValidator`) drops `analyzed_from_source` observations whose `file` is not in the trace ReadSet; `block_condition` relaxation for observation-emitting judges. |
| p0151c | #162 | Passive observation bus visible to downstream skills via `ObservationBusProjector` and an "Observations So Far" section in `ApiSkillPromptStrategy`. |
| p0151d | #163 | Per-pipeline cost cap (`PipelineCostCapConfig` + `IsBudgetExhausted` on `PipelineCostTracker`) — when reached, `SkillCallRuntime` short-circuits remaining LLM calls with a typed `cost-cap-exhausted` observation. |
| p0151e | — | Killed. Body-section enforcement would have re-opened the dual-format problem p0127c hard-cut over. Source-anchoring rule lives in the system prompt prefix (p0151a) + runtime validator (p0151b); not in 44+ SKILL.md files. |
| p0151f | #165 (companion) + skills #39 | api-security catalog rewrite: 5 inventory survey skills + jwt-validation-judge (with explicit drop-if) + report-synthesizer. Three existing source-reviewer judges unblocked by p0151b. |
| p0151g | #164 | Scanner anchors preserved in a structured top-N (`ScannerTopFindings`) alongside the prose summary. Deterministic per-scanner selectors (Nuclei / ZAP / Spectral). |
| p0151h | #166 | `AnchoringVerifier` runs at output-render time; surfaces orphan / source-claim failures in the operator-facing summary's Verification block. Reference snapshot doc captures the qualitative bar + anti-checklist. |
| p0151i | — (parked) | Dispatch protocol for adaptive follow-up rounds. Deferred per the umbrella's own decision: implement only if the smoke shows passive observation bus alone (p0151c) does not hit the IDE-buddy quality bar. |

## Pipeline-agnostic infrastructure

a, b, c, d, g, h are pipeline-agnostic — every pipeline that runs skills inherits them automatically (`fix-bug`, `implement-feature`, `security-scan`, `api-security-scan`, future pipelines). The proving-ground for the umbrella's thesis is api-security-scan (p0151f), but the infrastructure changes apply uniformly.

## Open question for the operator

Does the passive observation bus (p0151c) alone hit the IDE-buddy quality bar on the reference target, or does it need the dispatch protocol (p0151i)? The first smoke run after merging all eight PRs answers this. If the AnchoringVerifier reports all-pass with non-trivial findings, passive is enough and p0151i stays parked. If the verifier reports orphans that the new catalog could not anchor without follow-up rounds, p0151i moves to active/.

## Cross-cutting decisions worth remembering

- **No new SKILL.md format.** Every behavior the umbrella requires lives in the runtime (trace + validator) or in the prompt-strategy preamble. SKILL.md files stay at the p0127c format.
- **Decentralised verification over central recon.** Every skill in the new catalog has its own tools and grounds its own claims. The bus is a hint, not a contract.
- **Anchor enforcement is mechanical.** `analyzed_from_source` observations are dropped at parse time if the cited file is not in the skill call's trace ReadSet. There is no soft path; the contract is enforced.
- **Cost cap on PerSkillBreakdown, not a parallel signal.** p0132a/b made `PerSkillBreakdown` the single source of cost truth; the cap reads from the same totals.
- **Verification is descriptive, not blocking.** A failed anchoring assertion produces a `FAIL` marker in the summary; it does not abort the pipeline run. The bar surfaces; the operator decides.
