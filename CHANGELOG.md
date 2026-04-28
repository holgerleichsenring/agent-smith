# Changelog

## [0.24.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.23.0...v0.24.0) (2026-04-28)


### Features

* externalize skill catalog into agentsmith-skills repo (p0103) ([4a51511](https://github.com/holgerleichsenring/agent-smith/commit/4a5151175339cccd44730d59e224fdbc5f2138c1))
* externalize skill catalog into agentsmith-skills repo (p0103) ([aba039d](https://github.com/holgerleichsenring/agent-smith/commit/aba039dfe9f6ce6763fa7815e5a8beb2880bbc07))
* **prompts:** finish IPromptCatalog migration — all prompts as embedded resources (p0103c) ([83a9741](https://github.com/holgerleichsenring/agent-smith/commit/83a97417a340adc459df80daceb23edf9438197c))
* **prompts:** IPromptCatalog foundation + drop project-vision (p0103c partial) ([1268bf9](https://github.com/holgerleichsenring/agent-smith/commit/1268bf9bb4fbbfec386f5ac093de89e32092bd23))

## [0.23.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.22.0...v0.23.0) (2026-04-27)


### Features

* api-scan resolves source: block automatically (p0102a) ([0eb24d5](https://github.com/holgerleichsenring/agent-smith/commit/0eb24d572ad24178183cb2a5392e1f00409ddb54))


### Bug Fixes

* minify JSON output instructions to avoid gate truncation ([0557631](https://github.com/holgerleichsenring/agent-smith/commit/0557631fe047b8904d6805f7faee56e96c75ece6))

## [0.22.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.21.2...v0.22.0) (2026-04-27)


### Features

* parallel skill rounds with batch fan-out (p0097) ([63f2ce1](https://github.com/holgerleichsenring/agent-smith/commit/63f2ce1cdd3f536d2ca190e7eaee21751d07dda4))

## [0.21.2](https://github.com/holgerleichsenring/agent-smith/compare/v0.21.1...v0.21.2) (2026-04-27)


### Bug Fixes

* **deps:** bump Microsoft.Extensions.* from 10.0.6 to 10.0.7 ([a76fab7](https://github.com/holgerleichsenring/agent-smith/commit/a76fab7b8255476ef890849cd4bbc3cce16c0fbf))

## [0.21.1](https://github.com/holgerleichsenring/agent-smith/compare/v0.21.0...v0.21.1) (2026-04-27)


### Bug Fixes

* CLI commands work without Redis (security-scan, fix, mad, ...) (p0101 follow-up) ([3fd93ef](https://github.com/holgerleichsenring/agent-smith/commit/3fd93eff15780a33156522edb74f9049519c6c87))

## [0.21.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.20.0...v0.21.0) (2026-04-27)


### Features

* polling honors pipeline_from_label like webhooks (p0099a) ([6e68122](https://github.com/holgerleichsenring/agent-smith/commit/6e68122219696003313bcddae98b5e9bf781b41d))
* server stays up + reports per-subsystem health when Redis is missing (p0101) ([bc1563b](https://github.com/holgerleichsenring/agent-smith/commit/bc1563bac4edafcf20cb60dc34ec7d5de14ec899))

## [0.20.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.19.0...v0.20.0) (2026-04-26)


### Features

* event pollers + leader election — GitHub poller, housekeeping-under-leader (p96) ([c0d0fb0](https://github.com/holgerleichsenring/agent-smith/commit/c0d0fb0091eb90c8854facc2bad7aef6a3e30ca9))
* Gitignore-aware SourceFileEnumerator — LibGit2Sharp replaces hardcoded excludes (p94a) ([9d82aab](https://github.com/holgerleichsenring/agent-smith/commit/9d82aab0e7208a74bccbbf06c9b6c24627efd4a0))
* K8s manifests restructure — Kustomize to flat numbered YAMLs (p92) ([2329cc5](https://github.com/holgerleichsenring/agent-smith/commit/2329cc50d230d6fc6d53bd7281c801754c362d57))
* LLM output error handling — no silent catches, corrective retry, explicit input_categories (p93) ([73eb6c5](https://github.com/holgerleichsenring/agent-smith/commit/73eb6c528e8ed9b127bb9f55fbf385351eda8060))
* multi-platform status transitioners + webhook unification (p95b) ([6649185](https://github.com/holgerleichsenring/agent-smith/commit/66491857307e5334d2b3c434d767f64f00746300))
* PlanConsolidator cleanup — structured output, no fake Plan, fatal failures (p90) ([f439f45](https://github.com/holgerleichsenring/agent-smith/commit/f439f45afa9f8efa10c12d7e977222eec9bb37a1))
* Security-scan skill reduction 15 → 9 — overlap removal, IDOR patterns, legacy dict removed (p94b) ([118fccc](https://github.com/holgerleichsenring/agent-smith/commit/118fccccdf5fb3d3421e3de42a2e4f82b5af2f4a))
* ticket lifecycle recovery — heartbeat, stale detector, reconciler (p95c) ([09d3947](https://github.com/holgerleichsenring/agent-smith/commit/09d394771ffbd6ad09b0dba017aab517fead74e1))
* ticket-claim spine for GitHub — queue, lock, transitioner, consumer (p95a) ([f51ef05](https://github.com/holgerleichsenring/agent-smith/commit/f51ef05286abb0bf1b3123fa2ea2debe440a47cf))

## [0.19.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.18.0...v0.19.0) (2026-04-17)


### Features

* configurable defaults, skill content improvements, prompt extraction infra (p88, p89a, p89b) ([8cd9413](https://github.com/holgerleichsenring/agent-smith/commit/8cd941375d89751407ec2d2228a3b5fd130ca27f))

## [0.18.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.17.0...v0.18.0) (2026-04-17)


### Features

* nullable branch in CheckoutAsync — LocalSourceProvider uses HEAD when no branch specified (p74) ([365db04](https://github.com/holgerleichsenring/agent-smith/commit/365db046cf374279b318a9515643cbee9f01b987))

## [0.17.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.16.0...v0.17.0) (2026-04-16)


### Features

* ticket image attachments for LLM vision + restore gate skills (p87) ([73b3352](https://github.com/holgerleichsenring/agent-smith/commit/73b3352a42028acc101745fcf3f6fe91fb2fdf64))

## [0.16.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.15.3...v0.16.0) (2026-04-16)


### Features

* attacker-perspective skills + unified webhook lifecycle (p79, p80, p84) ([e6cdea0](https://github.com/holgerleichsenring/agent-smith/commit/e6cdea03a2705f30c7a9259739f30f3b462b6213))
* **p78:** gate category routing — filter input, merge output ([fd114b9](https://github.com/holgerleichsenring/agent-smith/commit/fd114b9fb6b9525a0c8191210bba27ad48fe9a1e))
* **p83:** Jira webhook status lifecycle — trigger gate, done transition, comment trigger ([d935e59](https://github.com/holgerleichsenring/agent-smith/commit/d935e5963bed6bb4be9f890c3a72f5c6c7220c2e))
* **p85:** webhook structured dispatch — PipelineRequest instead of free-text ([1c215b8](https://github.com/holgerleichsenring/agent-smith/commit/1c215b89081fd25d3dfa6cd85e41ca9450979934))
* typed inter-agent communication — SkillObservation replaces free-text DiscussionLog (p86) ([a4f77b8](https://github.com/holgerleichsenring/agent-smith/commit/a4f77b8daa638dc26374c642720059c5a4ef0d3a))


### Bug Fixes

* ConsolidatedPlan is context for plan generation, not a replacement ([85a49b4](https://github.com/holgerleichsenring/agent-smith/commit/85a49b430c8c4ccc43d4f9be8a1b9be773fb5781))
* **docs:** move site_dir outside docs_dir to prevent recursive copy ([98ece38](https://github.com/holgerleichsenring/agent-smith/commit/98ece382c4fdef3f59dcfc4bcd495a81938a5128))
* remove unused logger parameter from TeamsTypedQuestionTracker ([ff8869e](https://github.com/holgerleichsenring/agent-smith/commit/ff8869ed04f4b6fd30d00de94ae8c7d23364bd19))
* set Plan in context when consolidated by multi-role discussion ([5f389ad](https://github.com/holgerleichsenring/agent-smith/commit/5f389ad25d25c794305779b07257d370db5006b2))
* upgrade KubernetesClient 16.0.7 → 17.0.14 (NU1902 vulnerability) ([6386845](https://github.com/holgerleichsenring/agent-smith/commit/6386845f90239cc6acaf78cd3d9b21959b2b169e))

## [0.15.3](https://github.com/holgerleichsenring/agent-smith/compare/v0.15.2...v0.15.3) (2026-04-15)


### Bug Fixes

* prevent YAML anchor error in context.yaml generation ([9948c8e](https://github.com/holgerleichsenring/agent-smith/commit/9948c8ee689b007768d11d83fc2216dfed5faf9d))

## [0.15.2](https://github.com/holgerleichsenring/agent-smith/compare/v0.15.1...v0.15.2) (2026-04-15)


### Bug Fixes

* cost display says 'no pricing configured' instead of misleading 'local/free' ([d481c02](https://github.com/holgerleichsenring/agent-smith/commit/d481c0285b6cfff2fde68d2c360bdb307ae65b71))
* markdown findings as sections instead of truncated table ([4423f9a](https://github.com/holgerleichsenring/agent-smith/commit/4423f9a2d811ad2d79e8b8bd8bb2f646a04a4248))
* **p77:** false-positive-filter must output ALL retained findings ([26067c3](https://github.com/holgerleichsenring/agent-smith/commit/26067c3b155f143efb6b4a30e263f81331ea8f36))
* **p77:** gate with 0 confirmed findings is OK, not a veto ([9f51482](https://github.com/holgerleichsenring/agent-smith/commit/9f51482d621c70eecfeb3036e8a9f46bedb43ee1))
* **p77:** merge dast-false-positive-filter into false-positive-filter ([58856c5](https://github.com/holgerleichsenring/agent-smith/commit/58856c57b48f580e4263692e25a834e8173359d2))
* **p77:** PipelineCostTracker uses config pricing instead of hardcoded map ([406d252](https://github.com/holgerleichsenring/agent-smith/commit/406d252588f9183ec3be6e673943ea9f202c9789))

## [0.15.1](https://github.com/holgerleichsenring/agent-smith/compare/v0.15.0...v0.15.1) (2026-04-15)


### Bug Fixes

* **p77:** resolve skills_path relative to config file directory ([eb83e2f](https://github.com/holgerleichsenring/agent-smith/commit/eb83e2f6c2cb2f1aead5dedf6db2d3e29c723772))

## [0.15.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.14.1...v0.15.0) (2026-04-15)


### Features

* **p77:** fix skills_path, ZAP exit codes, container permissions, debug logging ([df9a617](https://github.com/holgerleichsenring/agent-smith/commit/df9a6177c6a23173a8fffb4f7f7cf3413f6b50a4))

## [0.14.1](https://github.com/holgerleichsenring/agent-smith/compare/v0.14.0...v0.14.1) (2026-04-15)


### Bug Fixes

* improve security finding output — no more :0 locations, add Details column ([d610f9f](https://github.com/holgerleichsenring/agent-smith/commit/d610f9fab6a5c57e8f883bedeba06852407ee150))

## [0.14.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.13.2...v0.14.0) (2026-04-15)


### Features

* **p76:** per-deployment model routing for Azure OpenAI ([b55dcdf](https://github.com/holgerleichsenring/agent-smith/commit/b55dcdf09b15e3ab27aa0b3c715fad689a487267))


### Bug Fixes

* **p76:** readable HTTP error messages + verbose logging for Azure OpenAI ([4958fb0](https://github.com/holgerleichsenring/agent-smith/commit/4958fb019add47b7ee6ed1220a41608faa6192bf))

## [0.13.2](https://github.com/holgerleichsenring/agent-smith/compare/v0.13.1...v0.13.2) (2026-04-14)


### Bug Fixes

* **p76:** register azure-openai in LlmClientFactory + add Azure auth to OpenAiCompatibleClient ([334f609](https://github.com/holgerleichsenring/agent-smith/commit/334f60940537c27aa409171bd5133182df2c7a3f))

## [0.13.1](https://github.com/holgerleichsenring/agent-smith/compare/v0.13.0...v0.13.1) (2026-04-14)


### Bug Fixes

* LocalSourceProvider checkout existing branch instead of crashing ([47cb27a](https://github.com/holgerleichsenring/agent-smith/commit/47cb27a443630cd7b37680e4989711525d86c9cb))

## [0.13.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.12.1...v0.13.0) (2026-04-14)


### Features

* **p76:** Azure OpenAI agent provider ([87956ba](https://github.com/holgerleichsenring/agent-smith/commit/87956ba9c39122c7d332dca0444113c45526e373))


### Bug Fixes

* **p76:** use AZURE_OPENAI_API_KEY as default secret name ([7a199e0](https://github.com/holgerleichsenring/agent-smith/commit/7a199e0722abbc47f2b2f04fafb2f832136c0c91))

## [0.12.1](https://github.com/holgerleichsenring/agent-smith/compare/v0.12.0...v0.12.1) (2026-04-14)


### Bug Fixes

* bump Microsoft.Extensions packages 10.0.5 → 10.0.6 ([f435ace](https://github.com/holgerleichsenring/agent-smith/commit/f435aceec32ea997f3ff806ba2f0101234eb7d6f))

## [0.12.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.11.0...v0.12.0) (2026-04-14)


### Features

* **p73:** rewrite coding principles — responsibility modeling ([95dc036](https://github.com/holgerleichsenring/agent-smith/commit/95dc036541a4c92ce33d6edb604b0068689d6ff5))
* **p74:** CLI source overrides — --source-type, --source-path, --source-url, --source-auth ([2cf2b36](https://github.com/holgerleichsenring/agent-smith/commit/2cf2b3643ccb151997e367b0ee72dac6af1669fb))
* **p74:** CLI source overrides — --source-type, --source-path, --source-url, --source-auth ([85bffdd](https://github.com/holgerleichsenring/agent-smith/commit/85bffdd870d345aff81781d6815e6f97148eace3))
* **p75:** add phase-spec.schema.json + schema refs to all phase YAMLs ([b93d5f3](https://github.com/holgerleichsenring/agent-smith/commit/b93d5f3b8d573075d9b0aa77457bfdbbf4e8d112))
* **p75:** convert all 93 phase docs from markdown to compact YAML ([1a47604](https://github.com/holgerleichsenring/agent-smith/commit/1a476041bb42906f66e391b2d8a11e6278803101))

## [0.11.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.10.1...v0.11.0) (2026-04-14)


### Features

* **p73:** add static-vs-instance coding principle ([6236f9c](https://github.com/holgerleichsenring/agent-smith/commit/6236f9c73fec0156957953cdf88e1cf32b496875))

## [0.10.1](https://github.com/holgerleichsenring/agent-smith/compare/v0.10.0...v0.10.1) (2026-04-13)


### Bug Fixes

* add missing --output-dir option to security-scan command ([a789a5c](https://github.com/holgerleichsenring/agent-smith/commit/a789a5cde5cc5b080eaec4daebd8df8d92789b5f))

## [0.10.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.9.0...v0.10.0) (2026-04-12)


### Features

* add --dry-run flag to autonomous, compile-wiki, and security-trend commands ([b1e1207](https://github.com/holgerleichsenring/agent-smith/commit/b1e12076cef5d89bc853a875c89284777a3df522))
* add CodingSoul blog and community links to docs site ([8fab6f0](https://github.com/holgerleichsenring/agent-smith/commit/8fab6f0b06f222a94ef46a63871e83db6d17c1c4))
* add green dot SVG favicon ([64e21ac](https://github.com/holgerleichsenring/agent-smith/commit/64e21ac6fef74c97ea063e116ee028f2cb4a0575))
* add landing page website for Vercel deployment ([3161ab6](https://github.com/holgerleichsenring/agent-smith/commit/3161ab624c67084669afda2703430a0d90b1e239))
* add logo to docs — hero image on landing page, theme logo/favicon ([9813ad3](https://github.com/holgerleichsenring/agent-smith/commit/9813ad39d62d012efdae3c502ca7603f80e71bc2))
* animated pipeline demo with typewriter effect ([43afab7](https://github.com/holgerleichsenring/agent-smith/commit/43afab71d4a3185f9b604bac70e72a7c841a7af7))
* auto-scroll pipeline demo as lines overflow ([b925095](https://github.com/holgerleichsenring/agent-smith/commit/b92509500910e41956cc297fa39620eab7458d27))
* close all DoD gaps for p58/p59/p60 ([a3c52f5](https://github.com/holgerleichsenring/agent-smith/commit/a3c52f538fc041d24a95d197a6f2982a02416fac))
* complete Phase 55+56 — SARIF output, provider enrichment, docs update ([6a91b49](https://github.com/holgerleichsenring/agent-smith/commit/6a91b4995845df8121c0f7e0bbd667cbe15ff606))
* how-it-works carousel with per-pipeline steps and terminals ([9f15e44](https://github.com/holgerleichsenring/agent-smith/commit/9f15e44362b2f9068d7e31af8ab560561c75a29a))
* implement p57b (skill manager) + p61 (knowledge base) ([a83f033](https://github.com/holgerleichsenring/agent-smith/commit/a83f033489529ebdd19dfbefac8cf8ca9e2a16ee))
* implement p58 steps 3-7, p59 steps 4-6 — adapters, tools, webhook handler ([262d577](https://github.com/holgerleichsenring/agent-smith/commit/262d577c359b2404147b0168d3ee2497dffac41b))
* implement p58/p59/p60 foundation — dialogue, webhooks, security enhancements ([ea50fc6](https://github.com/holgerleichsenring/agent-smith/commit/ea50fc63d014ef5e0e08f67132e751ab6f3e105b))
* implement p59 steps 7-10, p60 step 7 — dialogue routing + auto-fix ([9efa162](https://github.com/holgerleichsenring/agent-smith/commit/9efa162ae6a936b2414bdbae8f583fd46de981fb))
* **p57a:** migrate all 33 skills from YAML to SKILL.md format ([4c4e102](https://github.com/holgerleichsenring/agent-smith/commit/4c4e102a2b8aa320a854811af2a2b414ebed6f5f))
* **p57c:** implement autonomous pipeline — agent writes tickets ([43be21d](https://github.com/holgerleichsenring/agent-smith/commit/43be21dd48d71e512ed8a63781141839408088dc))
* **p58b:** Microsoft Teams integration with Adaptive Cards ([941b4de](https://github.com/holgerleichsenring/agent-smith/commit/941b4def874862305607c1f6c07d9647de64f61c))
* **p59b, p59c:** GitLab MR + Azure DevOps PR comment webhooks ([997d88b](https://github.com/holgerleichsenring/agent-smith/commit/997d88b4fe4326dcc7f028ed5c0f5999b05506fa))
* **p63:** structured finding assessment — close LLM-to-output gap ([69d1627](https://github.com/holgerleichsenring/agent-smith/commit/69d162776455ef217f209004c7b66533d79c0ee0))
* **p64:** typed skill orchestration — deterministic execution graph ([3436073](https://github.com/holgerleichsenring/agent-smith/commit/34360733ba30d1ac5b135dc066d592fd1c9e4cdd))
* **p65:** redesign website to Vercel/Geist style ([9200209](https://github.com/holgerleichsenring/agent-smith/commit/9200209968ed96cb59449410c6a5ad8d7db809c9))
* **p66:** self-documentation & multi-agent orchestration docs, Linear design tokens ([c602a63](https://github.com/holgerleichsenring/agent-smith/commit/c602a63bb44f89081dc2f35107c34e7b372c28a1))
* **p67:** API scan finding compression + ZAP fix ([3b2001c](https://github.com/holgerleichsenring/agent-smith/commit/3b2001c3955c7b00d189ce35d0c871f0a1ec6823))
* **p68:** add ApiPath/SchemaName to Finding for API scan locations ([e5a0322](https://github.com/holgerleichsenring/agent-smith/commit/e5a0322de1bcea642da8a6d209eb932dcf8a8feb))
* **p70:** decision log with phase/run context ([0f8938d](https://github.com/holgerleichsenring/agent-smith/commit/0f8938df86902c5d7630e668d9ca47505b2a9577))
* **p71:** Jira assignee webhook trigger ([2bda403](https://github.com/holgerleichsenring/agent-smith/commit/2bda4032c5c1442949d2c4ed5fb0995e84e3dd73))
* Phase 53 - documentation site with MkDocs Material ([c749413](https://github.com/holgerleichsenring/agent-smith/commit/c7494139447fbeeeb912de8e4a2983605e63ac4f))
* Phase 54 - security scan expansion with static patterns, git history, dependency audit ([07be630](https://github.com/holgerleichsenring/agent-smith/commit/07be6309831e195b546785fd995d1b03cba41e40))
* Phase 55+56 — findings compression, severity logs, mandatory FP filter, secret providers ([76ad3ab](https://github.com/holgerleichsenring/agent-smith/commit/76ad3abf4eea06b36965124e06dcf4fc035d20df))
* **website:** add Sentinel link to nav, CTA, and footer ([280d89a](https://github.com/holgerleichsenring/agent-smith/commit/280d89a843a8598eaac74b6f4c5dd0f668977f0b))
* **website:** update security scan to 18 steps, 9 skills ([ebcc2e0](https://github.com/holgerleichsenring/agent-smith/commit/ebcc2e017ca480ebe386274c55abd4026477df40))


### Bug Fixes

* all pipelines use correct default skills via PipelinePresets mapping ([4474594](https://github.com/holgerleichsenring/agent-smith/commit/44745946f4c5c8ac4d39ec7daca4cdc42f94a6a9))
* clean up landing page — remove duplicate logo/title, left-aligned hero ([490af34](https://github.com/holgerleichsenring/agent-smith/commit/490af34c843cae17720ea22e789754f3d1ccb75b))
* community link → GitHub Issues ([c75d25c](https://github.com/holgerleichsenring/agent-smith/commit/c75d25c847268cffd6f7d5ebeb85adabcf7b691b))
* complete all pipeline steps + fix auto-scroll ([4da7d1f](https://github.com/holgerleichsenring/agent-smith/commit/4da7d1f5c9a23f1d89ce2898ffe158bb762f4413))
* config file discovery via AGENTSMITH_CONFIG_DIR for single-file binary ([76da6b9](https://github.com/holgerleichsenring/agent-smith/commit/76da6b903fdaee8738f3fd21d08b3858df794ebb))
* **docs:** improve code block readability in light mode ([7880e9e](https://github.com/holgerleichsenring/agent-smith/commit/7880e9e014ab62e14210524de3e0ba6d6eef8774))
* **docs:** replace emoji shortcodes with Unicode emojis ([3cdfc89](https://github.com/holgerleichsenring/agent-smith/commit/3cdfc89ee2d87cc18a17654aa2836cf45839da2c))
* false positive ([877ab46](https://github.com/holgerleichsenring/agent-smith/commit/877ab46df981eb8ece9041642318598f0c5ee518))
* how-it-works tabs not switching — CSS specificity conflict ([f97fdd2](https://github.com/holgerleichsenring/agent-smith/commit/f97fdd2dd2196a5f0617c907a94c1bf219cc41a3))
* make hero logo full content width ([33a2798](https://github.com/holgerleichsenring/agent-smith/commit/33a2798486cfe72410cc4cc7163ab5cf0f71361d))
* **p63:** add Critical + ReviewStatus to finding summary and console output ([a23b567](https://github.com/holgerleichsenring/agent-smith/commit/a23b56740070ede15a8481138aab73f495e06f72))
* **p64:** gate findings flow into ExtractFindings, exclude .md from scanner, update docs ([f00d4f1](https://github.com/holgerleichsenring/agent-smith/commit/f00d4f12c7c3227515819c449d5bd8590c1297b3))
* **p64:** improve deterministic triage logging — show stage roles and execution order ([b95ddd8](https://github.com/holgerleichsenring/agent-smith/commit/b95ddd8ca6c87608720d2db79abfd59ed32a4fa4))
* **p64:** snapshot uses gate-filtered findings, add structured pipeline debug logs ([9213f94](https://github.com/holgerleichsenring/agent-smith/commit/9213f94962d7d2cdf8db44ed8518077d89aa7b3e))
* **p67:** create work dir in tar archive before docker cp ([7f6c778](https://github.com/holgerleichsenring/agent-smith/commit/7f6c778143f98b10506dbd96126b0423beaffa50))
* **p67:** inject target URL into swagger servers for ZAP ([036f547](https://github.com/holgerleichsenring/agent-smith/commit/036f54771975a539e5f5895d586f58b92230bf53))
* **p67:** set ZAP work dir to /zap/wrk — fix 'directory not mounted' error ([5cda015](https://github.com/holgerleichsenring/agent-smith/commit/5cda015a4046180b5532e32175bac04072279477))
* **p68:** handle empty string apiPath/schemaName from LLM output ([7cf851e](https://github.com/holgerleichsenring/agent-smith/commit/7cf851e7834fcfc42fce5ba1e341612e9614d3c8))
* **p71:** update docs workflow for mkdocs.yml at docs/mkdocs.yml ([7392995](https://github.com/holgerleichsenring/agent-smith/commit/7392995a38de8dbcefb2f2181bbcd7deca9ac092))
* register PatternDefinitionLoader in DI container ([cb91ac6](https://github.com/holgerleichsenring/agent-smith/commit/cb91ac67976297c730042652cbbb672c5fd3079f))
* remove PublishSingleFile from csproj — breaks Docker build ([59b94b8](https://github.com/holgerleichsenring/agent-smith/commit/59b94b832e98f97b9d0529fff340dab95d302a07))
* remove the word free from link ([07bc8ec](https://github.com/holgerleichsenring/agent-smith/commit/07bc8ec5f6f782714d66cd4ce7e2353f5d4e211b))
* replace grid cards with table — grid cards require Material Insiders ([21b6bd4](https://github.com/holgerleichsenring/agent-smith/commit/21b6bd49d034c5d8c1f340b37826b7f627a26b29))
* resolve patterns directory from config/ in working dir, not bin/ ([47c7ab1](https://github.com/holgerleichsenring/agent-smith/commit/47c7ab12fe335c3beb8174867931b651bb903213))
* restore 33 skills to subdirectory structure, fix pipeline and delivery bugs ([4e9e396](https://github.com/holgerleichsenring/agent-smith/commit/4e9e396cd14a28891cdbff9bdd83f2b01de4911a))
* security-scan loads security skills instead of coding skills ([ec70e7c](https://github.com/holgerleichsenring/agent-smith/commit/ec70e7c2bf7ea34ce89b91352a8062b2fd82f89a))
* **test:** use unique env var name to avoid CI GITHUB_TOKEN collision ([06c950a](https://github.com/holgerleichsenring/agent-smith/commit/06c950a75614935ecb3b5a99873c604af5daa650))
* use AppContext.BaseDirectory instead of Assembly.Location for Spectral ruleset ([131af7f](https://github.com/holgerleichsenring/agent-smith/commit/131af7f5af171903f69c73c3de0e98217ba9930c))
* **website:** add missing MAD steps 7-9 ([b55f63f](https://github.com/holgerleichsenring/agent-smith/commit/b55f63f101660d93a4c8e46346476a901c4e8c07))
* wire SpawnZap, SecurityTrend, SpawnFix into pipeline presets ([6fa238a](https://github.com/holgerleichsenring/agent-smith/commit/6fa238a6dfbb8790018a54bfb813d19a675bdaeb))
