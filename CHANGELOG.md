# Changelog

## [0.71.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.70.1...v0.71.0) (2026-05-31)


### Features

* **pipeline:** repo-prefix awareness + Redis ProjectMap cache + TicketFetched event (p0179h+p0182+p0184) ([#248](https://github.com/holgerleichsenring/agent-smith/issues/248)) ([a2c73d3](https://github.com/holgerleichsenring/agent-smith/commit/a2c73d3f9bff04e882778ec9ecbacdfe31d227c5))

## [0.70.1](https://github.com/holgerleichsenring/agent-smith/compare/v0.70.0...v0.70.1) (2026-05-31)


### Bug Fixes

* **prompts:** SkillCatalogPromptCatalog reads from {Root}/skills (p0179g) ([#246](https://github.com/holgerleichsenring/agent-smith/issues/246)) ([bcfc87d](https://github.com/holgerleichsenring/agent-smith/commit/bcfc87dce6cfc5fafc86da49fee35b4807f05d8e))

## [0.70.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.69.1...v0.70.0) (2026-05-31)


### Features

* **dashboard:** execution-tree run-detail + system-page redesign (p0183) ([#244](https://github.com/holgerleichsenring/agent-smith/issues/244)) ([fc3e8f4](https://github.com/holgerleichsenring/agent-smith/commit/fc3e8f4a96c1ad38f97c4063d62bfffed01086f9))

## [0.69.1](https://github.com/holgerleichsenring/agent-smith/compare/v0.69.0...v0.69.1) (2026-05-30)


### Bug Fixes

* **pipeline:** make Plan optional on Approval so collapsed coding presets run (p0179f) ([#242](https://github.com/holgerleichsenring/agent-smith/issues/242)) ([7e9b71c](https://github.com/holgerleichsenring/agent-smith/commit/7e9b71cabb3fae6017067bdd3addfd05417b7b84))

## [0.69.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.68.1...v0.69.0) (2026-05-30)


### Features

* **server:** version in banner + raw-API diagnostic in ListDirectoryAsync ([#239](https://github.com/holgerleichsenring/agent-smith/issues/239)) ([573e4ed](https://github.com/holgerleichsenring/agent-smith/commit/573e4ed26fcf38c54ffd89dbf6ba28a14e0e466d))

## [0.68.1](https://github.com/holgerleichsenring/agent-smith/compare/v0.68.0...v0.68.1) (2026-05-30)


### Bug Fixes

* **azure-repos:** normalise leading slash in ListDirectoryAsync filter ([#237](https://github.com/holgerleichsenring/agent-smith/issues/237)) ([46105ed](https://github.com/holgerleichsenring/agent-smith/commit/46105ed21faf8b7960a19c6c15f6eb9216b2199b))

## [0.68.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.67.0...v0.68.0) (2026-05-29)


### Features

* **sandbox:** dedupe per toolchain image — repo with N csharp contexts gets 1 container (p0180) ([#235](https://github.com/holgerleichsenring/agent-smith/issues/235)) ([99c3776](https://github.com/holgerleichsenring/agent-smith/commit/99c37763841200d18f39b9f479c8a25180763ac4))


### Bug Fixes

* **azure-repos:** honor catalog default_branch (parity with GitHub/GitLab) ([#234](https://github.com/holgerleichsenring/agent-smith/issues/234)) ([9d82037](https://github.com/holgerleichsenring/agent-smith/commit/9d82037398c2981451d146a613c03179aaeb6223))
* **bootstrap:** make discovery + probe diagnostic visible at Info level ([07fdaf5](https://github.com/holgerleichsenring/agent-smith/commit/07fdaf5cf802a89b9fca5ff34f53dc2c211366cb))

## [0.67.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.66.0...v0.67.0) (2026-05-29)


### Features

* **config:** MaxConcurrentSubAgents + MaxSubAgentsPerRun (p0177 step 1) ([1172fb7](https://github.com/holgerleichsenring/agent-smith/commit/1172fb7c3ed6c71cf4ec190e305cd861bc589e9c))
* **dashboard:** typed sub-agent render + dimension filters (p0173f) ([576954c](https://github.com/holgerleichsenring/agent-smith/commit/576954c3c86348c55f98e8e9d2daaf3c8d5104a5))
* **dashboard:** typed sub-agent render + dimension filters (p0173f) ([ca4fb69](https://github.com/holgerleichsenring/agent-smith/commit/ca4fb69086f2382106009c6c27063ee0f2e3eb03))
* **events:** add IDomainEvent marker + DeprecatedFieldAttribute (p0173e step 1) ([f93c158](https://github.com/holgerleichsenring/agent-smith/commit/f93c15813b8fcd51eb004cec61f5c8863cfd3f0d))
* **events:** kill string commandName + ReportDetailAsync (p0173e steps 6+7) ([fdec3e3](https://github.com/holgerleichsenring/agent-smith/commit/fdec3e3ab1f9417f9a2c31632b5798c89ec26f47))
* **events:** typed message contracts + schema-evolution policy (p0173e) ([1f0d2e8](https://github.com/holgerleichsenring/agent-smith/commit/1f0d2e818bfb06995647755375469e6cbe1185b3))
* **loop:** extract agentic loop + sub-agent types + L2 events (p0177 steps 2-7) ([b180a56](https://github.com/holgerleichsenring/agent-smith/commit/b180a5614bffdfff3e945007a384614e0ad65226))
* **p0179a:** master prompts as role:master skills + adapter ([bf000af](https://github.com/holgerleichsenring/agent-smith/commit/bf000af98bfb2691bd0e17df44b0a15fce66f3c3))
* **p0179d+b:** coding + scan pipeline collapse to AgenticMaster ([#230](https://github.com/holgerleichsenring/agent-smith/issues/230)) ([1a07925](https://github.com/holgerleichsenring/agent-smith/commit/1a079259c895c4181aedc4020330184247bbd218))
* **p0179e:** mad-discussion collapse to AgenticMaster + p0177a spec ([#232](https://github.com/holgerleichsenring/agent-smith/issues/232)) ([e7fd6de](https://github.com/holgerleichsenring/agent-smith/commit/e7fd6de67af0aa84a51faf4cc320af9d91683a0b))
* **prompts:** revert p0177 step-11 sub-agent block (p0179a step 5) ([98133f2](https://github.com/holgerleichsenring/agent-smith/commit/98133f2d9e69417180f0f9f58b733f583341c33b))
* **prompts:** SkillCatalogPromptCatalog adapter (p0179a step 4) ([09d725d](https://github.com/holgerleichsenring/agent-smith/commit/09d725d502411e87cab68468b423c32d9b0f3073))
* **skills:** add Master role for cross-pipeline master skills (p0179a step 2) ([a41b4cf](https://github.com/holgerleichsenring/agent-smith/commit/a41b4cf6fdc7b7e29c3cee7948c5fda6b1781b46))
* **skills:** YamlSkillLoader picks up master skills from _masters/ (p0179a step 3) ([3650468](https://github.com/holgerleichsenring/agent-smith/commit/3650468e58dc78328f68480fa711008b9b01fdf9))
* **sub-agents:** master spawns typed sub-agents on shared loop (p0177) ([93b7f14](https://github.com/holgerleichsenring/agent-smith/commit/93b7f147f2ad4f1e621f132d69f68e4d04acd1bb))
* **sub-agents:** spawn/read tools + policy + tests + close (p0177 steps 8-12+verify) ([d09c1b0](https://github.com/holgerleichsenring/agent-smith/commit/d09c1b08b842ecd26f11b7c8bcdeec46178e7b40))

## [0.66.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.65.1...v0.66.0) (2026-05-28)


### Features

* call-scope attribution + per-repo cost in result.md (p0176a) ([b5e3130](https://github.com/holgerleichsenring/agent-smith/commit/b5e3130030183a7fb2442f8e79d6c57d193af8c5))
* cost-integrity baseline — events fire with real cost (p0176b) ([f5d8e5e](https://github.com/holgerleichsenring/agent-smith/commit/f5d8e5ee284939031b4a6a1e8ae97c1a353e5d96))
* p0175 dashboard bug fixes ([57fdf28](https://github.com/holgerleichsenring/agent-smith/commit/57fdf28f513a5a39ee431f17d99b314daf8da3ce))
* p0176b cost integrity ([748c370](https://github.com/holgerleichsenring/agent-smith/commit/748c37065d2065b28021a91439427fc309c18002))
* topology failure visibility (p0176c) ([0166888](https://github.com/holgerleichsenring/agent-smith/commit/01668886ce90f394a45059f7e9dd0f6e15c6d6cb))


### Bug Fixes

* **pipeline:** publish RunFinished(failed) on unhandled executor exception (p0175) ([cfc5efa](https://github.com/holgerleichsenring/agent-smith/commit/cfc5efa75f4f4dde8c34d62da1de23e74cabcc6b))

## [0.65.1](https://github.com/holgerleichsenring/agent-smith/compare/v0.65.0...v0.65.1) (2026-05-27)


### Bug Fixes

* **dashboard,server:** /system rollup + L3 details + 4 bug-fix bundle (p0175) ([00dae7b](https://github.com/holgerleichsenring/agent-smith/commit/00dae7bc7385091d49c5f8235a5af4fa5f0021d8))
* **dashboard,server:** /system rollup + L3 details + bug-fix bundle (p0175) ([662c0f7](https://github.com/holgerleichsenring/agent-smith/commit/662c0f7c2db1c43e6eac6db4b268ee7e939bb8fe))

## [0.65.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.64.0...v0.65.0) (2026-05-27)


### Features

* **dashboard:** /system route with operator-info cards (p0173d) ([88a5cfc](https://github.com/holgerleichsenring/agent-smith/commit/88a5cfc5ad89183a916af36f62972db36aa21dfb))
* **dashboard:** Activity tab + operator-vocabulary pills (p0169j-b) ([8197852](https://github.com/holgerleichsenring/agent-smith/commit/81978527118f7bd48efc5217caae352be6b91fee))
* **dashboard:** ArgoCD-style SVG topology graph (p0169j-d) ([877d673](https://github.com/holgerleichsenring/agent-smith/commit/877d6732ba3a9b902337c8dcfbf69d798ae7f306))
* **dashboard:** pull-cycle aggregation + DESIGN.md tokens (p0174) ([3375042](https://github.com/holgerleichsenring/agent-smith/commit/3375042af010e223d6c87ce68727b25303fb58ae))
* **dashboard:** pull-cycle aggregation + DESIGN.md tokens (p0174) ([3ad90ee](https://github.com/holgerleichsenring/agent-smith/commit/3ad90ee511168e4c3f2c16b1436147cfcd2c3afe))
* **dashboard:** Result tab via IRunArtifactStore 4th slot (p0169j-c) ([f4adb68](https://github.com/holgerleichsenring/agent-smith/commit/f4adb685f4cc4f187e53a868d8ac2e8d067df26b))
* **dashboard:** trail TTL 24h + expired empty-state (p0169j-a) ([bd3d02a](https://github.com/holgerleichsenring/agent-smith/commit/bd3d02ac0e3e7e321ef34a9a7b956c7b76a4c6de))
* **events:** chat + config + skill-catalog instrumentation (p0173c) ([f73dbc6](https://github.com/holgerleichsenring/agent-smith/commit/f73dbc6b24049afc992232f8c184c28d5a9bbffc))
* **events:** failure reasons as typed event fields (p0169j-b1) ([93ee372](https://github.com/holgerleichsenring/agent-smith/commit/93ee372b3b8d243ee8c8f4797b35e361d0ea76c7))
* **events:** poller + webhook system-event instrumentation (p0173b) ([7cadc55](https://github.com/holgerleichsenring/agent-smith/commit/7cadc5594cd82137cfff332f240d57796dfd3090))
* **events:** system event backbone (p0173a) ([cd6c518](https://github.com/holgerleichsenring/agent-smith/commit/cd6c5180d4c3ebf2fbb8c39e269f1495b7fd4144))
* p0162 p0171 phase plan ([7f29791](https://github.com/holgerleichsenring/agent-smith/commit/7f2979138cca756182c42657f05df1264fb14437))
* p0169j dashboard polish — Activity / Result / 24h trail / topology graph ([9008cf8](https://github.com/holgerleichsenring/agent-smith/commit/9008cf8cba1e8c7eba0c621ae3aa2becd510badc))
* p0173 operator info substrate — system events + /system dashboard ([add3e90](https://github.com/holgerleichsenring/agent-smith/commit/add3e9002c031b8c25c89cd1ad8ff060d5c44df5))


### Bug Fixes

* close PARKED items 3 + 4 (p0169j cleanup) ([8c13d77](https://github.com/holgerleichsenring/agent-smith/commit/8c13d77c22492eb6672dc93e47043411eb1c2a49))
* **config:** per_pipeline cost cap override now reaches the resolver ([fbd63d3](https://github.com/holgerleichsenring/agent-smith/commit/fbd63d3ffd6d1c33ec202fd85e513ceb367503ba))

## [0.64.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.63.0...v0.64.0) (2026-05-26)


### Features

* **landing:** publish lifecycle SVG redesign ([d0b77bf](https://github.com/holgerleichsenring/agent-smith/commit/d0b77bf5bc83bd391a03478b4d86adbadb57548e))
* **landing:** publish lifecycle SVG redesign (release trigger) ([0331220](https://github.com/holgerleichsenring/agent-smith/commit/03312201624c71ac85093fbf878fdd5e8f42008d))


### Bug Fixes

* **landing:** sync lifecycle SVG + replace overflowing branch labels with repo slugs ([0b8b310](https://github.com/holgerleichsenring/agent-smith/commit/0b8b310bdf3512ba343d00a7a52b8a24540e1abb))

## [0.63.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.62.1...v0.63.0) (2026-05-26)


### Features

* cold-init component discovery + per-context bootstrap writes + applies_to (p0161d) ([b63d9d9](https://github.com/holgerleichsenring/agent-smith/commit/b63d9d9f5cb8d5776e78c0a67a8b8f09dc8330f8))
* **landing:** lifecycle SVG — bidirectional agent loop + orchestrator-driven PRs ([98f4db9](https://github.com/holgerleichsenring/agent-smith/commit/98f4db959d5a51731bfb6e56476b4eec3c95a0de))
* spec first v2 migration ([b78b760](https://github.com/holgerleichsenring/agent-smith/commit/b78b760c58447270cf60a090134f5fa0a4196c7b))

## [0.62.1](https://github.com/holgerleichsenring/agent-smith/compare/v0.62.0...v0.62.1) (2026-05-23)


### Bug Fixes

* **docs:** broken intra-doc links + lifecycle SVG on docs home ([4fad32e](https://github.com/holgerleichsenring/agent-smith/commit/4fad32eef06772b53deff48e85d4cbd93788f141))
* **docs:** broken intra-doc links after p0160 page moves + SVG symlink → file ([fb431f9](https://github.com/holgerleichsenring/agent-smith/commit/fb431f9f3fed855e549c0c6abd99021885128cbd))

## [0.62.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.61.0...v0.62.0) (2026-05-23)


### Features

* **docs:** task-oriented restructure + Holger-voice rewrite (p0160) ([b6feb05](https://github.com/holgerleichsenring/agent-smith/commit/b6feb05fd81a8bf65f0cb78af5806a7e32932638))

## [0.61.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.60.1...v0.61.0) (2026-05-22)


### Features

* **design:** repo-root DESIGN.md + token pipeline (p0159a) ([f1aca75](https://github.com/holgerleichsenring/agent-smith/commit/f1aca759a6441501ecb2433364d7c0c0e5ac5d6d))
* **docs:** MkDocs Material re-skin + content drift fix (p0159c) ([52d1db9](https://github.com/holgerleichsenring/agent-smith/commit/52d1db9c316e1b9d3945a25cc478c70c77d2080a))
* **landing:** Eleventy re-skin against DESIGN.md tokens (p0159b) ([0d9407f](https://github.com/holgerleichsenring/agent-smith/commit/0d9407fce44c00a46067c54ce57f5a8f1775af75))

## [0.60.1](https://github.com/holgerleichsenring/agent-smith/compare/v0.60.0...v0.60.1) (2026-05-22)


### Bug Fixes

* **tickets:** atomic AzDO finalize to prevent TF26071 rev race ([91762e6](https://github.com/holgerleichsenring/agent-smith/commit/91762e6c2fb73865a10bad4632065d504d81fcb6))
* **tickets:** atomic AzDO finalize to prevent TF26071 rev race ([054af4f](https://github.com/holgerleichsenring/agent-smith/commit/054af4ff6549fc1e3ef4a3361cf36b0ffeb3b282))

## [0.60.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.59.0...v0.60.0) (2026-05-21)


### ⚠ BREAKING CHANGES

* **bootstrap:** per-repo BootstrapDispatch + BootstrapRound (p0158g)
* **sandbox:** per-repo sandbox + tool-layer path-prefix routing (p0158e)

### Features

* **bootstrap:** per-repo BootstrapDispatch + BootstrapRound (p0158g) ([9a53ab8](https://github.com/holgerleichsenring/agent-smith/commit/9a53ab8c4a0ee19015209898644968270fe9a442))
* **bootstrap:** per-repo gate + load + analyze (p0158f) ([f32addb](https://github.com/holgerleichsenring/agent-smith/commit/f32addb3d712bc629d53069d00216f557b643565))
* p0158 multi repo unified run ([660f847](https://github.com/holgerleichsenring/agent-smith/commit/660f84743070594a2eafd7eb6bd735b7569e0659))
* **sandbox:** per-repo sandbox + tool-layer path-prefix routing (p0158e) ([a5e6a03](https://github.com/holgerleichsenring/agent-smith/commit/a5e6a0395d9d80e2164215766685257d168f08b8))

## [0.59.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.58.1...v0.59.0) (2026-05-21)


### ⚠ BREAKING CHANGES

* pre-p0156 r{NN} run directories become invisible to wiki compilation. The clean switchover documented in p0156 spec.

### Features

* p0158 multi repo unified run ([#191](https://github.com/holgerleichsenring/agent-smith/issues/191)) ([d6fbde9](https://github.com/holgerleichsenring/agent-smith/commit/d6fbde952d96268806c81b389c233107efb25029))

## [0.58.1](https://github.com/holgerleichsenring/agent-smith/compare/v0.58.0...v0.58.1) (2026-05-20)


### Bug Fixes

* **sandbox-agent:** operator-grade logs — job scope, real command, single line ([#190](https://github.com/holgerleichsenring/agent-smith/issues/190)) ([2e9c02e](https://github.com/holgerleichsenring/agent-smith/commit/2e9c02ed507b3b3ea61aee07eb2882929c117794))
* **sandbox:** SandboxFileReader + TestHandler accept new ListFiles object shape ([#188](https://github.com/holgerleichsenring/agent-smith/issues/188)) ([54694c1](https://github.com/holgerleichsenring/agent-smith/commit/54694c17c8661354647048f9a039afe2bd193293))

## [0.58.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.57.2...v0.58.0) (2026-05-20)


### Features

* **tools:** MCP/Claude-Code parity for filesystem + shell surface (p0153) ([#187](https://github.com/holgerleichsenring/agent-smith/issues/187)) ([b950c14](https://github.com/holgerleichsenring/agent-smith/commit/b950c144f20c3c983d867d84e97a035021cfba64))
* **tools:** split overloaded grep/glob/list_files into explicit primitives ([#186](https://github.com/holgerleichsenring/agent-smith/issues/186)) ([73d1b5f](https://github.com/holgerleichsenring/agent-smith/commit/73d1b5f841bde64a0290578884aede2918818bd0))


### Bug Fixes

* **ado:** render markdown comments as HTML before posting to System.History ([#183](https://github.com/holgerleichsenring/agent-smith/issues/183)) ([b42145e](https://github.com/holgerleichsenring/agent-smith/commit/b42145ef2ed0d98b30570e737332a2ab5f6aaa95))
* **output:** meta-observation for empty skill responses + Code Findin… ([#181](https://github.com/holgerleichsenring/agent-smith/issues/181)) ([322af71](https://github.com/holgerleichsenring/agent-smith/commit/322af711cb697ff7178128f6513af45f2f4e5703))
* **sandbox:** GrepStepHandler accepts file paths, not just directories ([#185](https://github.com/holgerleichsenring/agent-smith/issues/185)) ([3f1b3db](https://github.com/holgerleichsenring/agent-smith/commit/3f1b3dbd9a46b5a416dc607aa2a24d36e1aee16d))
* **triggers:** drop DefaultPipeline fallback when pipeline_from_label is set ([#180](https://github.com/holgerleichsenring/agent-smith/issues/180)) ([fe36453](https://github.com/holgerleichsenring/agent-smith/commit/fe364533e49325fa445068beb98e7a554c7db894))

## [0.57.2](https://github.com/holgerleichsenring/agent-smith/compare/v0.57.1...v0.57.2) (2026-05-20)


### Bug Fixes

* **prompts:** reposition discussion-suffix JSON requirement so tool use survives ([#178](https://github.com/holgerleichsenring/agent-smith/issues/178)) ([103de9a](https://github.com/holgerleichsenring/agent-smith/commit/103de9aa472a2307e6e5ad6000a1aaf67d5708ca))

## [0.57.1](https://github.com/holgerleichsenring/agent-smith/compare/v0.57.0...v0.57.1) (2026-05-19)


### Bug Fixes

* **prompts:** pull preamble toward tool use via identity + concrete targets ([#175](https://github.com/holgerleichsenring/agent-smith/issues/175)) ([5fb9b83](https://github.com/holgerleichsenring/agent-smith/commit/5fb9b83c62fc7f354807646f1cce21fb82d2fa12))

## [0.57.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.56.1...v0.57.0) (2026-05-19)


### Features

* **prompts:** universal tool surface + evidence_mode contract in SourceAnchoringPreamble ([#173](https://github.com/holgerleichsenring/agent-smith/issues/173)) ([8d885ff](https://github.com/holgerleichsenring/agent-smith/commit/8d885ffb64d5e4372942d79ab777b68037cd8ba6))

## [0.56.1](https://github.com/holgerleichsenring/agent-smith/compare/v0.56.0...v0.56.1) (2026-05-19)


### Bug Fixes

* **observations:** downgrade mis-labeled analyzed_from_source observations instead of dropping ([#171](https://github.com/holgerleichsenring/agent-smith/issues/171)) ([ebb8ae6](https://github.com/holgerleichsenring/agent-smith/commit/ebb8ae6a40c14767cfeb61c9edc2f43971b6f965))

## [0.56.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.55.0...v0.56.0) (2026-05-19)


### Features

* **tools:** give skills bash + edit + glob + http_request + fix ReadSet capture ([#170](https://github.com/holgerleichsenring/agent-smith/issues/170)) ([7401e94](https://github.com/holgerleichsenring/agent-smith/commit/7401e94b6e4b65b35fbf8546c30bfa03ff869d08))


### Bug Fixes

* snake_case enum parsing end-to-end ([#159](https://github.com/holgerleichsenring/agent-smith/issues/159)) ([d69da48](https://github.com/holgerleichsenring/agent-smith/commit/d69da4835f39d47e5ed0327afe79049bbbbcb247))

## [0.55.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.54.0...v0.55.0) (2026-05-19)


### Features

* **p0151a:** wire LoopTraceCollector via decorators + enable RunCommand in Plan ([#160](https://github.com/holgerleichsenring/agent-smith/issues/160)) ([a47941c](https://github.com/holgerleichsenring/agent-smith/commit/a47941c957fc2073e68f093a8a0fa3726216f7a7))
* **p0151b:** snake_case EvidenceMode JSON, ReadSet-anchored validator, block_condition relaxation ([#161](https://github.com/holgerleichsenring/agent-smith/issues/161)) ([d81395c](https://github.com/holgerleichsenring/agent-smith/commit/d81395c4b8f105499455f57bb05ea4def84c1bfa))
* **p0151c:** passive observation bus visible to downstream skills ([#162](https://github.com/holgerleichsenring/agent-smith/issues/162)) ([6c96567](https://github.com/holgerleichsenring/agent-smith/commit/6c96567756607a424d1f0b70f6710a1c3a16f620))
* **p0151d:** per-pipeline cost cap hung on PerSkillBreakdown ([#163](https://github.com/holgerleichsenring/agent-smith/issues/163)) ([12cd6a4](https://github.com/holgerleichsenring/agent-smith/commit/12cd6a498084d3f7a241b53e4890514d0c1d7666))
* **p0151g:** preserve scanner anchors in structured top-N alongside summary ([#164](https://github.com/holgerleichsenring/agent-smith/issues/164)) ([20431d0](https://github.com/holgerleichsenring/agent-smith/commit/20431d00305c4030032fbf3c1c2de17d5d2b39d4))
* **p0151h:** IDE-buddy baseline validation via AnchoringVerifier ([#166](https://github.com/holgerleichsenring/agent-smith/issues/166)) ([ef7f47f](https://github.com/holgerleichsenring/agent-smith/commit/ef7f47f987dd06db22af04efc26096753b0b064d))


### Bug Fixes

* async scope teardown + nullable start_line + sandbox/tool-policy diag logging ([#168](https://github.com/holgerleichsenring/agent-smith/issues/168)) ([766a40f](https://github.com/holgerleichsenring/agent-smith/commit/766a40fd11c729828ff3f1fa22ad2f54f5ecfa2e))
* tolerant enum parsing for LLM observation fields + quieter polling ([#156](https://github.com/holgerleichsenring/agent-smith/issues/156)) ([53d9cc4](https://github.com/holgerleichsenring/agent-smith/commit/53d9cc49b88eb2fecca6120365ce44eb5f8ebe01))

## [0.54.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.53.0...v0.54.0) (2026-05-19)


### Features

* /smoke slash command for CLI + docker-compose smoke verification ([#149](https://github.com/holgerleichsenring/agent-smith/issues/149)) ([671662a](https://github.com/holgerleichsenring/agent-smith/commit/671662ab3bc0dba71f1fe3fa6495d72b3cd272ca))
* **p0146d:** drop ObservationParser regex-fishing, skills emit typed location fields ([f730009](https://github.com/holgerleichsenring/agent-smith/commit/f73000942c50dfc239679bfc71c9be25e04b5f46))
* **p0146d:** drop ObservationParser regex-fishing, skills emit typed location fields ([0413c90](https://github.com/holgerleichsenring/agent-smith/commit/0413c90dfaaeed225b626f30b90766af15c02aa8))
* **p0146e:** route PR-comment post-slash body through IIntentParser ([169a61e](https://github.com/holgerleichsenring/agent-smith/commit/169a61e4a79b78f253e81d84031736966d089e54))
* **p0146e:** route PR-comment post-slash body through IIntentParser ([ff68f3e](https://github.com/holgerleichsenring/agent-smith/commit/ff68f3ee866d8f7c2b9a07b91eb427406d32b16d))
* **p0147a:** consolidate LLM-JSON parsing through ITolerantJsonParser ([#153](https://github.com/holgerleichsenring/agent-smith/issues/153)) ([19fbee1](https://github.com/holgerleichsenring/agent-smith/commit/19fbee1258b282cc70bc107167a8643d1eb28aee))
* **p0147b:** surface skill execution-limit hits as typed observations ([5b17ac5](https://github.com/holgerleichsenring/agent-smith/commit/5b17ac5c4c5e7649a1abd15a2f2d4a275ab0ea01))
* **p0147b:** surface skill execution-limit hits as typed observations ([55d236b](https://github.com/holgerleichsenring/agent-smith/commit/55d236b21d1cc3e5c3f1cae72e824bc9d53636ec))
* **p0147c:** swagger compression + dynamic compaction trigger ([7fb6d32](https://github.com/holgerleichsenring/agent-smith/commit/7fb6d321ad484165b1af78aa005bbda31be2d6c3))
* **p0147c:** swagger spec compression + dynamic compaction trigger ([153198c](https://github.com/holgerleichsenring/agent-smith/commit/153198ca92bc8ab0635a5652342fb4e162a1fa91))
* **p0147d:** decompose SkillRoundHandlerBase via composition ([4d59d9c](https://github.com/holgerleichsenring/agent-smith/commit/4d59d9cccab677c6a3c85a9d61aedb6195468a1d))
* **p0147d:** decompose SkillRoundHandlerBase via composition ([f687e84](https://github.com/holgerleichsenring/agent-smith/commit/f687e844f5ab81e3ccb963e4b108764b4bd5cfda))
* **p0147e:** decompose PipelineExecutor into 3 sub-services ([c6525ec](https://github.com/holgerleichsenring/agent-smith/commit/c6525ecd42d1d092a157509f2e81a3d97e0f2d9e))
* **p0147f:** add ITicketFieldMapper contract + TicketProviderHttpClient ([7ec04e7](https://github.com/holgerleichsenring/agent-smith/commit/7ec04e741bfcf09b3215ff30333fac397552f9f2))
* **p0147f:** decompose AzureDevOpsTicketProvider (340 -&gt; 111 lines) ([985096c](https://github.com/holgerleichsenring/agent-smith/commit/985096c18d218e97134dd5d2d38189d99b93d49d))
* **p0147f:** decompose GitHubTicketProvider (219 -&gt; 114 lines) ([bec6e91](https://github.com/holgerleichsenring/agent-smith/commit/bec6e910f238fd881eefb6f079b16ed33dd05a72))
* **p0147f:** decompose GitLabTicketProvider (278 -&gt; 115 lines) ([4f4f7be](https://github.com/holgerleichsenring/agent-smith/commit/4f4f7beb7fabab85751be42b5a2cf76da8de076a))
* **p0147f:** decompose JiraTicketProvider (394 -&gt; 100 lines) ([9d6ba72](https://github.com/holgerleichsenring/agent-smith/commit/9d6ba7225062464ed17a331bd698f80596f3c3b1))
* **p0147f:** unify ticket provider family (Jira/ADO/GitLab/GitHub) ([dc74dc1](https://github.com/holgerleichsenring/agent-smith/commit/dc74dc18aee977ba61bb3ae779c8bd881f722ebc))
* **p0147g:** decompose round-handler family via injected business services ([#155](https://github.com/holgerleichsenring/agent-smith/issues/155)) ([cb78a01](https://github.com/holgerleichsenring/agent-smith/commit/cb78a014d7830b1213a0b7a10a99b9932c1340d1))
* **p0147h:** split 6 oversize files into per-subdomain partials ([48b8a2a](https://github.com/holgerleichsenring/agent-smith/commit/48b8a2adc12ab474d019e70c00602d726ec358ec))
* **p0147h:** split 6 oversize files into per-subdomain partials ([32d720d](https://github.com/holgerleichsenring/agent-smith/commit/32d720dfd3ba7121a9f868960a1f9af5e683f06b))
* **p0148:** route skill rounds through ISkillRoundToolPolicy ([#150](https://github.com/holgerleichsenring/agent-smith/issues/150)) ([39fe9c8](https://github.com/holgerleichsenring/agent-smith/commit/39fe9c826ec6ddbc52cfc99401588b808383252f))
* **p0149:** migrate DI to feature-set extension methods ([#151](https://github.com/holgerleichsenring/agent-smith/issues/151)) ([ea89101](https://github.com/holgerleichsenring/agent-smith/commit/ea891015b52b9f1271cb96876ca32ffbd44c8826))
* **p0150a:** delete PipelineExecutorLegacy + collapse tests to single-shape ([#152](https://github.com/holgerleichsenring/agent-smith/issues/152)) ([ade3553](https://github.com/holgerleichsenring/agent-smith/commit/ade3553f637ab27a8662dde71eea9b2ecd33167e))
* **p0150b:** decompose ObservationParser + ProjectAnalyzer + ConvergenceCheckHandler ([#154](https://github.com/holgerleichsenring/agent-smith/issues/154)) ([fa0b93c](https://github.com/holgerleichsenring/agent-smith/commit/fa0b93cecf6ea28fdbfe66fcfbddd978d4032a56))


### Bug Fixes

* merge conflicts ([e527766](https://github.com/holgerleichsenring/agent-smith/commit/e527766fd1bb3c48f64e15cc513ac4fdcf901cec))
* repair main after parallel-merge fallout ([#147](https://github.com/holgerleichsenring/agent-smith/issues/147)) ([6f2c3aa](https://github.com/holgerleichsenring/agent-smith/commit/6f2c3aa90242c216191dceee1e82bee34b421ac4))

## [0.53.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.52.0...v0.53.0) (2026-05-19)


### Features

* delete ApiCodeContext + regex extractors (p0146a) ([35d5a7b](https://github.com/holgerleichsenring/agent-smith/commit/35d5a7b5fa321b9dde7eecc26ecd0471cda310cb))
* delete regex intelligence layer for api-security (p0146a) ([be09651](https://github.com/holgerleichsenring/agent-smith/commit/be096514f7b2714857e3d4034c37795299a00249))
* drop convergence/objection prose-regex fallback (p0146c) ([6f0bee7](https://github.com/holgerleichsenring/agent-smith/commit/6f0bee739d6de3c227da19db341efb43ae849df0))
* drop convergence/objection prose-regex fallback (p0146c) ([8ecdbb2](https://github.com/holgerleichsenring/agent-smith/commit/8ecdbb2c5aef698cf1e82df6043a964c0e714338))
* retire RegexIntentParser (p0146b) ([a5d7e74](https://github.com/holgerleichsenring/agent-smith/commit/a5d7e74c696444788c22580203efb4b7ff20f67a))
* retire RegexIntentParser (p0146b) ([05a5980](https://github.com/holgerleichsenring/agent-smith/commit/05a5980d169aa277741ae4970d581f1a352f1523))


### Bug Fixes

* add Critical to ObservationSeverity (stops chain-analyst silent drops) ([ea38d68](https://github.com/holgerleichsenring/agent-smith/commit/ea38d6863003eaa4abc3b79b2ef5b0dfff18930f))
* add Critical to ObservationSeverity so chain-analyst output stops dropping ([a19b08b](https://github.com/holgerleichsenring/agent-smith/commit/a19b08b02b2b57d823e524438fe74ad5fe308e04))
* combine class-level [Route] with method [Http*] in DotNetRouteExtractor ([900a343](https://github.com/holgerleichsenring/agent-smith/commit/900a34340e682af978c08c0686bf36cd59229185))
* drop CommandExecutor success traces to Debug to remove log noise ([5f0af3b](https://github.com/holgerleichsenring/agent-smith/commit/5f0af3bbc26eeca7eb9c4401b0e6d72444c152a9))
* preserve UTF-8 BOM convention across read/write in FileStepHandler ([ba20f5f](https://github.com/holgerleichsenring/agent-smith/commit/ba20f5ff7532b4ff22fd6b87fec3dafeaf0e090a))
* unify YAML config schema on snake_case + close p0140a area_path bug ([4743e34](https://github.com/holgerleichsenring/agent-smith/commit/4743e34f8441d2e6caf6a517cf769b006f169a68))
* unify YAML config schema on snake_case + close p0140a area_path bug ([d9b250d](https://github.com/holgerleichsenring/agent-smith/commit/d9b250d9a8783de89c200b753eaae51323f80650))

## [0.52.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.51.0...v0.52.0) (2026-05-18)


### Features

* p0140 multi repo and project pipelines ([32f3b8f](https://github.com/holgerleichsenring/agent-smith/commit/32f3b8fb062c4da3dbd2fe2d480bf2c67015d3cb))
* unfence autonomous + skill-manager presets (p0144) ([6347c25](https://github.com/holgerleichsenring/agent-smith/commit/6347c25c58f3cff19873ff5d31712782ba226635))


### Bug Fixes

* increase agent smith skills version ([e8cac4a](https://github.com/holgerleichsenring/agent-smith/commit/e8cac4ad432b121ceb8ace51bb46eb41e81fe6ac))

## [0.51.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.50.2...v0.51.0) (2026-05-18)


### Features

* introduce named catalogs in agentsmith.yml (p0139) ([c7eaa24](https://github.com/holgerleichsenring/agent-smith/commit/c7eaa24c48e9b5836c1ab9fea167efe2abef2934))
* metrics + empty-plan gate + docs closeout (p0140e) ([48f8e40](https://github.com/holgerleichsenring/agent-smith/commit/48f8e4047316191e1b631fd5f8a2ae1b930f9783))
* p0140 multi repo and project pipelines ([7c17eaf](https://github.com/holgerleichsenring/agent-smith/commit/7c17eaf2f9ebba66bf36facd269c7d431602a5eb))
* p0140 multi repo and project pipelines ([059bf0a](https://github.com/holgerleichsenring/agent-smith/commit/059bf0a6f23e9e41d919112fdbe6caa397475d45))
* per-tracker pollers + deprecation of project-level polling (p0140c) ([24a0b8a](https://github.com/holgerleichsenring/agent-smith/commit/24a0b8aaa03e6211228053eab84de614836f084f))
* pipeline-scoped ToolKit + IToolHost decomposition (p0145) ([e7b0fa1](https://github.com/holgerleichsenring/agent-smith/commit/e7b0fa19240977c2da5013452caa10fbac3bbdc7))
* project resolver foundation slice (p0140a) ([7f31fb7](https://github.com/holgerleichsenring/agent-smith/commit/7f31fb73a80381263b15cb84d1ba4cb5743ca7e4))
* ResolvedProject.Repo shim removal + ContextKeys.CurrentRepo (p0140d) ([c5d72c9](https://github.com/holgerleichsenring/agent-smith/commit/c5d72c9b1c1a52f1b6e214e213b8132395df24e0))
* SkillCallRuntime consumer migration + HitLimit + gate cost scope (p0142) ([135a8a1](https://github.com/holgerleichsenring/agent-smith/commit/135a8a1b8d7d2c08777be39655c210184350b2d8))
* webhook migration + multi-repo spawner (p0140b) ([2d9332c](https://github.com/holgerleichsenring/agent-smith/commit/2d9332cca81dc99b1cabe5903c9a8c8f57a445e2))

## [0.50.2](https://github.com/holgerleichsenring/agent-smith/compare/v0.50.1...v0.50.2) (2026-05-15)


### Bug Fixes

* raise triage rationale cap 500 → 1000 chars (api-security-scan) ([15e78f4](https://github.com/holgerleichsenring/agent-smith/commit/15e78f42a5fa5219fdae362900ec930489b1b97d))
* raise triage rationale cap 500 → 1000 chars (api-security-scan) ([a79a664](https://github.com/holgerleichsenring/agent-smith/commit/a79a664f2033c6d75c725a1fb19ee533355e03f4))

## [0.50.1](https://github.com/holgerleichsenring/agent-smith/compare/v0.50.0...v0.50.1) (2026-05-15)


### Bug Fixes

* CLI api-scan source→sandbox handoff (BootstrapCheck false negati… ([152c0cd](https://github.com/holgerleichsenring/agent-smith/commit/152c0cd5ee905f673338c9ae8e4508c02a758aed))
* CLI api-scan source→sandbox handoff (BootstrapCheck false negatives) ([7eb07d1](https://github.com/holgerleichsenring/agent-smith/commit/7eb07d1c7cc5968fc347feeac147a24e04529f68))

## [0.50.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.49.1...v0.50.0) (2026-05-15)


### Features

* rewrite triage prompt + pin skills v2.1.0 (p0138) ([6a7065f](https://github.com/holgerleichsenring/agent-smith/commit/6a7065f09c72828c282c0e3f09d6f99551a33fd4))
* rewrite triage prompt + pin skills v2.1.0 (p0138) ([18da074](https://github.com/holgerleichsenring/agent-smith/commit/18da074f105da42d0f22e955febbdc5a157a594d))

## [0.49.1](https://github.com/holgerleichsenring/agent-smith/compare/v0.49.0...v0.49.1) (2026-05-14)


### Bug Fixes

* give dispatcher user a real home directory in Server Dockerfile ([b1d2d02](https://github.com/holgerleichsenring/agent-smith/commit/b1d2d025ae3264bae1dc952cd80fd821454ff517))
* give dispatcher user a real home directory in Server Dockerfile ([d411747](https://github.com/holgerleichsenring/agent-smith/commit/d4117471b1b66433937491c948973df11eff78c4))

## [0.49.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.48.1...v0.49.0) (2026-05-14)


### Features

* DI hygiene — single AddHttpClient + typed clients + ValidateScopes (p0137b) ([9ec65bf](https://github.com/holgerleichsenring/agent-smith/commit/9ec65bfb8c7a0777a8f44fb6a87677af1a6da32f))
* drop Task.Run wrappers in hosted services + RedisEventChannel Start (p0137c) ([93bf69f](https://github.com/holgerleichsenring/agent-smith/commit/93bf69f32916debcc26ae876d295e3ec15dab4f5))
* log filters to appsettings + compose tag pinning (p0137d) ([bfe7c8f](https://github.com/holgerleichsenring/agent-smith/commit/bfe7c8f5341864b8afc5fc08fdff0283e2f62540))
* orchestrator-spawn-path configuration symmetry (p0137a) ([7f0f8ab](https://github.com/holgerleichsenring/agent-smith/commit/7f0f8ab3c82cd125e814abb600256128b1d845d9))

## [0.48.1](https://github.com/holgerleichsenring/agent-smith/compare/v0.48.0...v0.48.1) (2026-05-14)


### Bug Fixes

* configurable sandbox agent image (registry + version) via agents… ([d1d7c24](https://github.com/holgerleichsenring/agent-smith/commit/d1d7c247ec1bdbc4412bdc56545a9ef36b55ed4c))
* configurable sandbox agent image (registry + version) via agentsmith.yml ([7b21acd](https://github.com/holgerleichsenring/agent-smith/commit/7b21acdb4e85dd1ef75d714bc8d75c90ae617b5b))

## [0.48.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.47.0...v0.48.0) (2026-05-14)


### Features

* configurable KubernetesJobSpawner resources + central env-var constants (p0136 follow-up) ([76e8b93](https://github.com/holgerleichsenring/agent-smith/commit/76e8b93e45b76b8fab2b76071e3cbeef8bf3c8f6))
* p0136 per project sandbox resources ([2842b8b](https://github.com/holgerleichsenring/agent-smith/commit/2842b8b66a29d28266be608901c17325797993ea))
* per-project sandbox container resources via IOptions (p0136) ([d03553e](https://github.com/holgerleichsenring/agent-smith/commit/d03553e3285c8b7ad74f82fa9be1625db20de4f1))

## [0.47.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.46.0...v0.47.0) (2026-05-13)


### Features

* inject GitHub + AzDo SDK clients via factory (p0135 follow-up) ([72e1bc5](https://github.com/holgerleichsenring/agent-smith/commit/72e1bc57555a11feac2ca311996b4590befdb801))
* inject GitHub + AzDo SDK clients via factory (p0135 follow-up) ([3937a9c](https://github.com/holgerleichsenring/agent-smith/commit/3937a9c68cbc7d8b6c5ef6c46802f01b781230b6))

## [0.46.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.45.2...v0.46.0) (2026-05-12)


### Features

* sandbox toolchain auto-detection (p0135) ([2ca1c71](https://github.com/holgerleichsenring/agent-smith/commit/2ca1c7155c7636638222705d1d106e1b8c7e0b09))
* sandbox toolchain auto-detection (p0135) ([f6f95f9](https://github.com/holgerleichsenring/agent-smith/commit/f6f95f90b5ff9ff5f0d4dda3365682b655f58e0f))

## [0.45.2](https://github.com/holgerleichsenring/agent-smith/compare/v0.45.1...v0.45.2) (2026-05-12)


### Bug Fixes

* **k8s:** pod-watcher detects init-container failures + leaves pod for inspection ([#104](https://github.com/holgerleichsenring/agent-smith/issues/104)) ([759410f](https://github.com/holgerleichsenring/agent-smith/commit/759410fea3b93ef0c0613a7f83053ff33d64e1ca))
* **k8s:** set CPU + memory limits on agent-loader initContainer ([#105](https://github.com/holgerleichsenring/agent-smith/issues/105)) ([99f9648](https://github.com/holgerleichsenring/agent-smith/commit/99f964892c4619a086222af9c3071ca611d33c13))

## [0.45.1](https://github.com/holgerleichsenring/agent-smith/compare/v0.45.0...v0.45.1) (2026-05-12)


### Bug Fixes

* **k8s:** set CPU + memory limits on agent-loader initContainer ([#102](https://github.com/holgerleichsenring/agent-smith/issues/102)) ([e40f6e0](https://github.com/holgerleichsenring/agent-smith/commit/e40f6e07784dee3feb16849e92c3426ba9cffa98))

## [0.45.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.44.0...v0.45.0) (2026-05-12)


### Features

* p0133 init project label trigger ([#100](https://github.com/holgerleichsenring/agent-smith/issues/100)) ([a57f2aa](https://github.com/holgerleichsenring/agent-smith/commit/a57f2aa0eb5c1d20e0ebe31a605b03a7dc3c78cf))

## [0.44.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.43.0...v0.44.0) (2026-05-12)


### Features

* p0133 init project label trigger ([#98](https://github.com/holgerleichsenring/agent-smith/issues/98)) ([eeb8348](https://github.com/holgerleichsenring/agent-smith/commit/eeb8348ee522fdcf320ccddee79fcca4480fcc8b))

## [0.43.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.42.0...v0.43.0) (2026-05-11)


### Features

* label-triggered init-project onboarding (p0133) ([#96](https://github.com/holgerleichsenring/agent-smith/issues/96)) ([f5692be](https://github.com/holgerleichsenring/agent-smith/commit/f5692be9990ce85f35b90e4c66052bafd80bf4fd))

## [0.42.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.41.0...v0.42.0) (2026-05-11)


### Features

* label-triggered init-project onboarding (p0133) ([#94](https://github.com/holgerleichsenring/agent-smith/issues/94)) ([f393aea](https://github.com/holgerleichsenring/agent-smith/commit/f393aead97db24125305e6e81420d3593ee6acd9))


### Bug Fixes

* **sandbox:** translate Step.WorkingDirectory '/work' to local temp dir in InProcessSandbox ([#93](https://github.com/holgerleichsenring/agent-smith/issues/93)) ([3ffd91f](https://github.com/holgerleichsenring/agent-smith/commit/3ffd91f6fa140a1deca393b9813df47d6c8611eb))

## [0.41.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.40.1...v0.41.0) (2026-05-10)


### ⚠ BREAKING CHANGES

* SkillMdParser drops the legacy code path; only the new single-body format with role-as-frontmatter and activates_when loads. Pairs synchronously with agent-smith-skills 2.0.0 release.

### Features

* D4-D7 + p0132 — concept-vocabulary, verify-phase, init-project, cleanup, cost-attribution ([#91](https://github.com/holgerleichsenring/agent-smith/issues/91)) ([7261bed](https://github.com/holgerleichsenring/agent-smith/commit/7261bedaebd4137db084aea173dd96ce000a4ecb))

## [0.40.1](https://github.com/holgerleichsenring/agent-smith/compare/v0.40.0...v0.40.1) (2026-05-10)


### Bug Fixes

* **triage:** accept any concept-vocabulary key as valid rationale ([#89](https://github.com/holgerleichsenring/agent-smith/issues/89)) ([b4050d2](https://github.com/holgerleichsenring/agent-smith/commit/b4050d2187e358b2d5ebf8a6d9efb29c386c79dd))

## [0.40.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.39.1...v0.40.0) (2026-05-07)


### Features

* **p0123:** SkillObservation as universal pipeline output (Finding retired) ([#84](https://github.com/holgerleichsenring/agent-smith/issues/84)) ([5fb7e0a](https://github.com/holgerleichsenring/agent-smith/commit/5fb7e0a75e82021123e7754b834f92eacd96708c))


### Bug Fixes

* **filter:** preserve observations when filter LLM response is unparseable ([#87](https://github.com/holgerleichsenring/agent-smith/issues/87)) ([29a364b](https://github.com/holgerleichsenring/agent-smith/commit/29a364b4fb8514788a0412d9839c02287f1bb962))
* **triage:** tolerate skills with no activation criteria + clear build warnings ([#86](https://github.com/holgerleichsenring/agent-smith/issues/86)) ([71243b1](https://github.com/holgerleichsenring/agent-smith/commit/71243b197737f25d1c9a06a5c10ca84ad8217dd1))

## [0.39.1](https://github.com/holgerleichsenring/agent-smith/compare/v0.39.0...v0.39.1) (2026-05-07)


### Bug Fixes

* **p0117b:** create sandbox for every sandbox-routed handler ([#79](https://github.com/holgerleichsenring/agent-smith/issues/79)) ([612afff](https://github.com/holgerleichsenring/agent-smith/commit/612afffc413b37199759bc3b96ad03efbd760bdc))
* **pipeline:** cap skill-round chain depth + break A→B→A cycles ([#80](https://github.com/holgerleichsenring/agent-smith/issues/80)) ([d949e3d](https://github.com/holgerleichsenring/agent-smith/commit/d949e3d1181badad98b36a6c3778b4a956eedd2c))

## [0.39.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.38.0...v0.39.0) (2026-05-06)


### Features

* **p0117b:** sandbox consumer migration — LibGit2Sharp out, Repository.LocalPath const, deeper services sandbox-routed ([#78](https://github.com/holgerleichsenring/agent-smith/issues/78)) ([1f69524](https://github.com/holgerleichsenring/agent-smith/commit/1f69524fb0e35545a75aca26fe6732daec57efe5))
* **p0117:** Sandbox follow-ups (Docker backend, TRX, sandbox-routed git ops, grep Step kind, Redis cleanup) ([#74](https://github.com/holgerleichsenring/agent-smith/issues/74)) ([b349957](https://github.com/holgerleichsenring/agent-smith/commit/b349957956d785c64b1b51ce7787d741e19ffe71))

## [0.38.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.37.3...v0.38.0) (2026-05-05)


### Features

* **p0119:** polymorphic cost-tracker + OpenAI cached_tokens demo fix ([#73](https://github.com/holgerleichsenring/agent-smith/issues/73)) ([212c88e](https://github.com/holgerleichsenring/agent-smith/commit/212c88eea5348fd05be5926cd5e8e257e4ccbb37))


### Bug Fixes

* **p0118a:** split CompactionEvent summarizer tokens into input + output for proper cost calc ([6e8d8ea](https://github.com/holgerleichsenring/agent-smith/commit/6e8d8ea7065cbbd36ced2ad5a30fe6db2ee04ccd))

## [0.37.3](https://github.com/holgerleichsenring/agent-smith/compare/v0.37.2...v0.37.3) (2026-05-05)


### Bug Fixes

* **p0118:** lifecycle MarkFailed on exceptions, TestHandler fail-without-sandbox, OpenAi summarizer-token attribution ([d6b06ce](https://github.com/holgerleichsenring/agent-smith/commit/d6b06cee265749c1e531822bfec858aa482577bb))

## [0.37.2](https://github.com/holgerleichsenring/agent-smith/compare/v0.37.1...v0.37.2) (2026-05-05)


### Reverts

* **p0113a:** roll back queue-dispatcher refactor + add p0115 sandbox spec ([#69](https://github.com/holgerleichsenring/agent-smith/issues/69)) ([4ce6bf7](https://github.com/holgerleichsenring/agent-smith/commit/4ce6bf705b54bd0288c2d5f992978d0b04026fd7))

## [0.37.1](https://github.com/holgerleichsenring/agent-smith/compare/v0.37.0...v0.37.1) (2026-05-05)


### Bug Fixes

* triage no-ticket fallback + robust libgit2 staging + scan-pipeline persist guard ([#66](https://github.com/holgerleichsenring/agent-smith/issues/66)) ([36ec2cc](https://github.com/holgerleichsenring/agent-smith/commit/36ec2ccc38011278870c2fe2a7718faf72c14459))

## [0.37.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.36.0...v0.37.0) (2026-05-04)


### Features

* combined p0112a + p0113a + p0114 — branch persistence + queue spawn + OpenAI compactor ([0963b02](https://github.com/holgerleichsenring/agent-smith/commit/0963b02a73099364a8431a6e5fa091417eeab3f7))
* **p0112a:** branch persistence MVP — push WIP commit on pipeline failure ([0211dfb](https://github.com/holgerleichsenring/agent-smith/commit/0211dfbf09a5b162d35dbdf2f0c9ed308d0dbdda))
* **p0113a:** spawn pipeline jobs from queue via ephemeral CLI containers ([c006e2e](https://github.com/holgerleichsenring/agent-smith/commit/c006e2ef4a3654df55dbbdf8c4b40a65a5417b11))
* **p0114:** OpenAi context compactor ([eb2a311](https://github.com/holgerleichsenring/agent-smith/commit/eb2a311854a5e416e9d4684f3c56904988d3af1d))


### Reverts

* **p0113a:** roll back queue-dispatcher refactor + add p0115 sandbox spec ([#65](https://github.com/holgerleichsenring/agent-smith/issues/65)) ([5d43cd7](https://github.com/holgerleichsenring/agent-smith/commit/5d43cd7b39338900362614472cde845d729ef984))

## [0.36.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.35.0...v0.36.0) (2026-05-04)


### Features

* **pipeline:** per-run correlation id + cost reporting on completion ([5924ae8](https://github.com/holgerleichsenring/agent-smith/commit/5924ae8b99b09211765e51da261434564b945317))


### Bug Fixes

* **analyzer:** tolerate trailing commas + line comments in model JSON ([65a8cfc](https://github.com/holgerleichsenring/agent-smith/commit/65a8cfc449f87b215dbf34474e724c16530a1df6))
* pipeline observability — correlation id, cost on failure, fail-fast on empty skill catalog ([8a4cb4b](https://github.com/holgerleichsenring/agent-smith/commit/8a4cb4b0a449f1ad21f026354dafb1f4ebedb7ea))
* **triage:** StructuredTriageStrategy fails fast when no skills loaded ([1b04921](https://github.com/holgerleichsenring/agent-smith/commit/1b04921cb3e4d313d9eb91e30c1263c2b8e374f5))

## [0.35.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.34.1...v0.35.0) (2026-05-04)


### Features

* **p0111a:** strict SkillLoader for extended frontmatter ([f617601](https://github.com/holgerleichsenring/agent-smith/commit/f6176018fd48ee4335081e604327783c82161b90))
* **p0111b:** spec set + skills.md callout + p0111 cleanup ([ec0d4c3](https://github.com/holgerleichsenring/agent-smith/commit/ec0d4c3fe3d811620ccb0a8f53505b3cfa8433a6))
* **p0111c:** phase-based triage and pipeline rewrite ([1bf57d3](https://github.com/holgerleichsenring/agent-smith/commit/1bf57d372786158a6ee925ff9f92f1fc90e38d8d))
* **p0111d:** per-provider SKILL.md overrides ([a69a54e](https://github.com/holgerleichsenring/agent-smith/commit/a69a54e2315de925a4ed04107eef3f951dc3c586))
* **p0111d:** per-provider SKILL.md overrides ([09c8688](https://github.com/holgerleichsenring/agent-smith/commit/09c8688b9d8b33792da22d9d5d6914dd45b10fcd))


### Bug Fixes

* CommandExecutor lifetime Singleton → Transient ([5639106](https://github.com/holgerleichsenring/agent-smith/commit/5639106a69fdf3301d72f4d61b902b1f18232568))
* CommandExecutor scope lifetime + bump skills catalog to v1.1.0 ([eb9cf66](https://github.com/holgerleichsenring/agent-smith/commit/eb9cf6666fcae18d4c887e13a6300435ff29ed39))

## [0.34.1](https://github.com/holgerleichsenring/agent-smith/compare/v0.34.0...v0.34.1) (2026-04-30)


### Bug Fixes

* **p0110b:** Azure OpenAI analyzer falls back to Planning task's deployment ([e29df1d](https://github.com/holgerleichsenring/agent-smith/commit/e29df1d4631ef0c7647bd5640e571f48ecdeb8d9))

## [0.34.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.33.0...v0.34.0) (2026-04-30)


### Features

* **p0109a:** decouple server-only lifecycle deps from application layer ([c331317](https://github.com/holgerleichsenring/agent-smith/commit/c331317cf0513d432ea75fc868631a71824f9671))
* **p0110b:** ProjectAnalyzer + ProjectMap ([f91a9cd](https://github.com/holgerleichsenring/agent-smith/commit/f91a9cdd96ebee78458b6df2f229a597c574230b))

## [0.33.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.32.1...v0.33.0) (2026-04-30)


### Features

* **p0109:** jira label-lock as decorator — CLI clear of IRedisClaimLock ([74080d3](https://github.com/holgerleichsenring/agent-smith/commit/74080d3867c98dfaf2e3127fc42fb627bea3ce67))
* **p0110a:** IAgenticAnalyzer abstraction + grep tool ([5d22ce8](https://github.com/holgerleichsenring/agent-smith/commit/5d22ce8d0ec55c437bd9542c257a5c256389d06e))

## [0.32.1](https://github.com/holgerleichsenring/agent-smith/compare/v0.32.0...v0.32.1) (2026-04-30)


### Bug Fixes

* **azdo:** replace tags atomically + HTML-format failure comments ([dc7f8c3](https://github.com/holgerleichsenring/agent-smith/commit/dc7f8c3cab0de5c3ffbc638ff844e4edcb804615))

## [0.32.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.31.0...v0.32.0) (2026-04-30)


### Features

* **logging:** per-ticket visibility for GitHub/GitLab/Jira providers + transitioners ([72f693c](https://github.com/holgerleichsenring/agent-smith/commit/72f693cd1878824ed8799f3cb158008114c28b7b))
* **p0108:** poller discovery — closes the webhook-equivalence gap ([c4444b8](https://github.com/holgerleichsenring/agent-smith/commit/c4444b84f2da655d5f4b5d71226e5a7ab06d7266))

## [0.31.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.30.0...v0.31.0) (2026-04-29)


### Features

* **logging:** per-ticket visibility across discovery → claim → enqueue ([3b948e1](https://github.com/holgerleichsenring/agent-smith/commit/3b948e1f026a3b7858705a3160493c229d7ed482))


### Bug Fixes

* **azdo:** VssConnection cache resilience — TTL + transport-failure eviction ([2a5e26f](https://github.com/holgerleichsenring/agent-smith/commit/2a5e26f0b0ac466af80f0de6fbefaa159b217548))


### Performance Improvements

* **azdo:** cache VssConnection process-wide; first-call ~10s no longer per-cycle ([9f658ad](https://github.com/holgerleichsenring/agent-smith/commit/9f658adca26b0a08c16fff130b8e730638548b5d))

## [0.30.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.29.0...v0.30.0) (2026-04-29)


### Features

* **logging:** poll-cycle heartbeat + factory + provider HTTP-call traces ([97f2910](https://github.com/holgerleichsenring/agent-smith/commit/97f2910b7520580e1259f2726bad01456f230c47))

## [0.29.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.28.3...v0.29.0) (2026-04-29)


### Features

* **logging:** compact single-line console formatter + entry-logs in polling chain ([9625da9](https://github.com/holgerleichsenring/agent-smith/commit/9625da957cd501462c57ae891c2af35d7266526c))

## [0.28.3](https://github.com/holgerleichsenring/agent-smith/compare/v0.28.2...v0.28.3) (2026-04-29)


### Bug Fixes

* **dialogue:** RedisDialogueTransport cancellation deterministic + test ([8f0bc7d](https://github.com/holgerleichsenring/agent-smith/commit/8f0bc7dd30e474a76b800dcdc56e813952b6a28a))

## [0.28.2](https://github.com/holgerleichsenring/agent-smith/compare/v0.28.1...v0.28.2) (2026-04-29)


### Bug Fixes

* **docker:** stop baking config/ into images; fail loudly when missing ([a4f4ec0](https://github.com/holgerleichsenring/agent-smith/commit/a4f4ec0498923c93068d21fe869e0eccf22d026d))
* **polling:** scope-leak in PollerLeaderHostedService + leader exceptions ([afcbb46](https://github.com/holgerleichsenring/agent-smith/commit/afcbb4615f9acdf459b32540e5e885197a66ef44))

## [0.28.1](https://github.com/holgerleichsenring/agent-smith/compare/v0.28.0...v0.28.1) (2026-04-29)


### Bug Fixes

* **lifecycle:** scanners fail-soft per project + startup summary log ([c905d84](https://github.com/holgerleichsenring/agent-smith/commit/c905d8432d256029f7482cea7b09f8afa8fce849))

## [0.28.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.27.2...v0.28.0) (2026-04-29)


### ⚠ BREAKING CHANGES

* **p0107:** The CLI's `server` subcommand has been removed. Users running `dotnet AgentSmith.Cli.dll server` (or the agentsmith-cli image with `command: ["server", ...]`) must switch to the agentsmith-server image. The two-container K8s deployment pattern is no longer needed — one Server container handles everything.

### Features

* **p0107:** server is now the single long-running deployment ([b347168](https://github.com/holgerleichsenring/agent-smith/commit/b347168e7b1a8831bcb06aa494f29cd277cb6b96))

## [0.27.2](https://github.com/holgerleichsenring/agent-smith/compare/v0.27.1...v0.27.2) (2026-04-29)


### Bug Fixes

* **p0106:** IAgentProviderFactory lifetime + decisions update ([1cf15c4](https://github.com/holgerleichsenring/agent-smith/commit/1cf15c41b3a583a7083306e2c6fa76e21661ca06))

## [0.27.1](https://github.com/holgerleichsenring/agent-smith/compare/v0.27.0...v0.27.1) (2026-04-29)


### Bug Fixes

* **p0106:** server DI registration + loosen trigger validation ([d608b4d](https://github.com/holgerleichsenring/agent-smith/commit/d608b4def33c237d50f9450bd80182b8cbc09853))

## [0.27.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.26.1...v0.27.0) (2026-04-29)


### Features

* **p0106:** multi-pipeline projects with per-pipeline overrides ([6e10787](https://github.com/holgerleichsenring/agent-smith/commit/6e107872aa7d451e71c583ca965907044cbfe894))
* **p0106:** multi-pipeline projects with per-pipeline overrides ([bed957b](https://github.com/holgerleichsenring/agent-smith/commit/bed957bf3beb88e458d19e790a43c0ea2e6ea068))

## [0.26.1](https://github.com/holgerleichsenring/agent-smith/compare/v0.26.0...v0.26.1) (2026-04-29)


### Bug Fixes

* **scan:** LoadSkillsHandler catalog resolution + summary on zero fin… ([c49c3ca](https://github.com/holgerleichsenring/agent-smith/commit/c49c3ca16e5c0c9ed4091d59c39766f1a7755a40))
* **scan:** LoadSkillsHandler catalog resolution + summary on zero findings ([6fc51e1](https://github.com/holgerleichsenring/agent-smith/commit/6fc51e135b29fce0fefc3ad31594e0e8c38ac1f7))

## [0.26.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.25.1...v0.26.0) (2026-04-29)


### Features

* **p0104:** api-scan project-brief + finding↔handler correlation + 2 new skills ([dd9fd1d](https://github.com/holgerleichsenring/agent-smith/commit/dd9fd1dadf5cd4ac137c54cbb055c9584a703753))
* **p0105:** security-scan adopts project-brief; retire LoadDomainRules name ([80acd9d](https://github.com/holgerleichsenring/agent-smith/commit/80acd9d329daa31050fac0e4da34739679593dc0))

## [0.25.1](https://github.com/holgerleichsenring/agent-smith/compare/v0.25.0...v0.25.1) (2026-04-28)


### Bug Fixes

* **config:** use snake_case cache_dir in YAML examples ([07849c6](https://github.com/holgerleichsenring/agent-smith/commit/07849c6ef3c45ecf8023d63d5989e173eaa2c526))
* **skills:** portable cache_dir default + snake_case YAML key ([63f7484](https://github.com/holgerleichsenring/agent-smith/commit/63f74846b87d63a18fd3e61bc3d8832f43ae1024))
* **skills:** portable default cache_dir, friendly permission-denied error ([846416a](https://github.com/holgerleichsenring/agent-smith/commit/846416ab471ae7ec903141d74d6422e9c1608ed7))

## [0.25.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.24.0...v0.25.0) (2026-04-28)


### Features

* **cli:** `agentsmith skills pull` reads config when flags omitted ([299915d](https://github.com/holgerleichsenring/agent-smith/commit/299915de9c3aef4e575fd16e68ea91c72d9abe5d))
* **security:** patterns ship via agentsmith-skills tarball; remove config/patterns/ ([ff19300](https://github.com/holgerleichsenring/agent-smith/commit/ff193003e51dfcf5f4f0593c8417b6dfde0a3ca0))


### Bug Fixes

* **dialogue:** run ReadLine on dedicated thread to avoid CI thread-pool starvation ([f180112](https://github.com/holgerleichsenring/agent-smith/commit/f18011206f63a81e35f751aca80526c8ecdb390d))
* **skills:** use correct GitHub repo URL holgerleichsenring/agent-smith-skills ([6fc1789](https://github.com/holgerleichsenring/agent-smith/commit/6fc17899b145efff191ebd142c59074e90bff300))

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
