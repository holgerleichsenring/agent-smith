# Changelog

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
