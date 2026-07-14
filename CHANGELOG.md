# Changelog

## [0.111.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.110.0...v0.111.0) (2026-07-14)


### Features

* context-level scoping (p0336b) ([8877606](https://github.com/holgerleichsenring/agent-smith/commit/8877606709613783763a0e7eccd8ec4eeef31293))
* context-level scoping (p0336b) ([9c1c43e](https://github.com/holgerleichsenring/agent-smith/commit/9c1c43e0a194d93f0e1f398aaf32f92cf8fa2886))


### Bug Fixes

* pipeline hardening — execute-prompt tokens, rate estimate, AzDO PR limit, plan-decision noise ([3a67dc6](https://github.com/holgerleichsenring/agent-smith/commit/3a67dc673a4ab5c5765675d23009fee8d9cfd72c))
* pipeline hardening (execute-prompt tokens, rate estimate, AzDO PR limit, plan-decision noise) ([1b8606e](https://github.com/holgerleichsenring/agent-smith/commit/1b8606ef73e80c51c203fa9a1582b34ca2f9a680))

## [0.110.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.109.1...v0.110.0) (2026-07-14)


### Features

* predictable capacity + run delete (p0336, p0337, p0338) ([164ce8c](https://github.com/holgerleichsenring/agent-smith/commit/164ce8c2469929e8bdd94be71841c5fd0c02ced4))
* predictable capacity + run delete (p0336, p0337, p0338) ([054957d](https://github.com/holgerleichsenring/agent-smith/commit/054957d956686d75e8106989a0da50ee85956226))

## [0.109.1](https://github.com/holgerleichsenring/agent-smith/compare/v0.109.0...v0.109.1) (2026-07-14)


### Bug Fixes

* release publish — keep RID/publish globals out of the skills-packaging tool (NETSDK1047) ([4609900](https://github.com/holgerleichsenring/agent-smith/commit/46099007dff0093b6ede7c942fde62af25559484))
* release publish no longer flows RID/publish globals into the skills-packaging tool ([c96facd](https://github.com/holgerleichsenring/agent-smith/commit/c96facd67fa324402ab04ae9fa4ddafe938c45d7))

## [0.109.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.108.0...v0.109.0) (2026-07-14)


### Features

* agent-smith demo — the whole loop on a bundled sample project (p0326) ([e846c32](https://github.com/holgerleichsenring/agent-smith/commit/e846c32e61eeb3ecd9cb4a8f389d494b140d3336))
* agent-smith doctor — active preflight checks for every silent-failure class (p0324) ([c33cf53](https://github.com/holgerleichsenring/agent-smith/commit/c33cf537c1652b938fff8eeb83eb683ca9b8a5d4))
* doctor, embedded skills, demo, durable dialogue, expectation negotiation + goldens, docs sweep (p0324-p0329, p0335) ([64351ac](https://github.com/holgerleichsenring/agent-smith/commit/64351ac81492b80ea283d40b85d82bd6db3ad7fc))
* durable dialogue — checkpoint at the ask, resume on the answer (p0327) ([215de76](https://github.com/holgerleichsenring/agent-smith/commit/215de76f25b1af954f7e239be79c76774fc11f49))
* expectation negotiation — ratified Soll block as the run's acceptance contract (p0328) ([b1c0e1a](https://github.com/holgerleichsenring/agent-smith/commit/b1c0e1a956915479e4b5a23c56fcb18c55140655))
* expectation replay goldens — eval mechanics, anonymized fixtures, ratification metrics (p0329) ([7e31d4b](https://github.com/holgerleichsenring/agent-smith/commit/7e31d4bb8d79f4101f1e4dc014749145191c8ef1))
* skills ship embedded in the release; pin becomes an override (p0325) ([3cdb0db](https://github.com/holgerleichsenring/agent-smith/commit/3cdb0db0379e82c2cb29d06d7f0149db71bebbd2))


### Bug Fixes

* ActivityRow + FilterRail handle ExpectationRatified (exhaustive event surfaces) ([658a42d](https://github.com/holgerleichsenring/agent-smith/commit/658a42dfb728e0e137c8ebf5a5a83078a2165342))
* mirror ExpectationRatifiedEvent into hub-events.ts (CI drift check) ([6c8112c](https://github.com/holgerleichsenring/agent-smith/commit/6c8112cfca9105fdc90a03f8bb3f19229be3d99b))
* working-status comment posts once per logical run, not per capacity relaunch ([9219650](https://github.com/holgerleichsenring/agent-smith/commit/92196503e5778f54a5f9802dc578e5cb536021ee))
* working-status ticket comment posts once per logical run, not per capacity relaunch ([bf7392c](https://github.com/holgerleichsenring/agent-smith/commit/bf7392c733b33cb54d925082d246e9587c4647c0))

## [0.108.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.107.1...v0.108.0) (2026-07-13)


### Features

* cancel enforcement, ticket-scoped provisioning, cost-honest capacity (p0330-p0332) ([d9bdc93](https://github.com/holgerleichsenring/agent-smith/commit/d9bdc93a6f41beeecc0dce5c565352954b8393a8))
* cancel is persistent state, enforced by durable force-kill (p0330 backend) ([9c9578a](https://github.com/holgerleichsenring/agent-smith/commit/9c9578a128a08c2c3cbf888fbd91b433a5b82a55))
* cancel state visible in every run state; queued runs cancellable (p0330 dashboard) ([ff2282a](https://github.com/holgerleichsenring/agent-smith/commit/ff2282aa05dce1077681e2f52c0d609ba1c34f90))
* cost-honest capacity — requests-based quota pin, honest defaults, reserved resource-time visible (p0332) ([81481d5](https://github.com/holgerleichsenring/agent-smith/commit/81481d59b5d198ac936f64979c32d1390b39a300))
* merge respects master read-set, stops laundering static-pattern FPs (p0333) ([f022abd](https://github.com/holgerleichsenring/agent-smith/commit/f022abd5ee1ec7d18125779abddd63a7c4f9e564))
* merge respects master read-set, stops laundering static-pattern FPs (p0333) ([84ad811](https://github.com/holgerleichsenring/agent-smith/commit/84ad811f0550822d944a48df6d2e1d80bdc944dd))
* recall + precision on security-scan — history-secret severity and generated-code skip (p0333b, p0333c) ([f569655](https://github.com/holgerleichsenring/agent-smith/commit/f569655f54001561a9c46057cbe664ffe52516ff))
* security-scan recall + precision — history-secret severity, generated-code skip (p0333b, p0333c) ([7f081a4](https://github.com/holgerleichsenring/agent-smith/commit/7f081a4ad4539c6d797a27abd8a46ed08d401805))
* ticket-scoped provisioning — scope first, spawn only whats needed, escalate on demand (p0331) ([61ef73b](https://github.com/holgerleichsenring/agent-smith/commit/61ef73b983c076cfd70c256cf6d706d5b4c32545))
* wire sandbox memory-request into created-event; p0330-p0332 recorded done ([32f5021](https://github.com/holgerleichsenring/agent-smith/commit/32f5021e415e10852cdd658a2636e837941b4ef7))

## [0.107.1](https://github.com/holgerleichsenring/agent-smith/compare/v0.107.0...v0.107.1) (2026-07-10)


### Bug Fixes

* **ci:** build dashboard image amd64-only ([b0c55ef](https://github.com/holgerleichsenring/agent-smith/commit/b0c55efa43f5215c6dacafbde22877852ee6596f))
* **ci:** build dashboard image amd64-only ([c9e5800](https://github.com/holgerleichsenring/agent-smith/commit/c9e580071101ec595423a9a455c30a6912237977))

## [0.107.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.106.0...v0.107.0) (2026-07-10)


### Features

* principles-first prefix order in agent-plan-system prompt (p0323) ([048cd2e](https://github.com/holgerleichsenring/agent-smith/commit/048cd2eb2316417644606fae6228ba682e6aa027))
* revive Anthropic prompt caching and surface cached share per LLM call (p0323) ([1304152](https://github.com/holgerleichsenring/agent-smith/commit/1304152e82ad3c1a36b5d7cdc4d21c66319b59d5))
* revive Anthropic prompt caching, surface cached share per LLM call (p0323) ([a76c440](https://github.com/holgerleichsenring/agent-smith/commit/a76c440955cf9187fb4c3c5cf85e9dae3ca793ac))


### Bug Fixes

* bootstrap dispatch uses authoritative sandbox-repo map; speaking context-name sandbox keys (p0322b) ([4267fa7](https://github.com/holgerleichsenring/agent-smith/commit/4267fa71957d6bf12f7f5a2e4e779053c621e324))
* no PR for run-record-only init diffs; real commit failures surface honestly (p0322c) ([2aafe8e](https://github.com/holgerleichsenring/agent-smith/commit/2aafe8e7d41f3a32c340ec7e9431f28c7eb24e2e))
* real x/y run progress, sandbox repo attribution + speaking keys, no run-record-only init PRs (p0322a-c) ([a25b585](https://github.com/holgerleichsenring/agent-smith/commit/a25b585e12ac395a1209b6369f27243bf1f0f565))
* runs list shows real x/y progress and init runs carry the ticket title (p0322a) ([9905759](https://github.com/holgerleichsenring/agent-smith/commit/9905759a9e3616ef78147284b1a774c011904857))

## [0.106.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.105.2...v0.106.0) (2026-07-10)


### Features

* capacity queue with FIFO position, pipeline-aware sandbox sizing, init re-run fix (p0320a-d, p0321) ([5194d5c](https://github.com/holgerleichsenring/agent-smith/commit/5194d5c98476cf7a08c7ccfa6ec2b02793d32156))
* persistent FIFO capacity queue with single-row queued runs (p0320c) ([c21caad](https://github.com/holgerleichsenring/agent-smith/commit/c21caada444dc9bfc291bb73b6378eb6eef36637))
* pipeline-aware sandbox sizing and full-footprint admission (p0320a, p0320b) ([dd26784](https://github.com/holgerleichsenring/agent-smith/commit/dd26784b02c1c3493d5b8ea0a6cc498ad49fc067))
* queued runs visible with FIFO position in the dashboard (p0320d) ([e620e05](https://github.com/holgerleichsenring/agent-smith/commit/e620e05e86d99c86432daa02fe8fee615864dfd1))


### Bug Fixes

* init-project terminalizes its ticket without requiring a PR (p0321) ([5cbdf5a](https://github.com/holgerleichsenring/agent-smith/commit/5cbdf5a7e89ecc80ef4049fa8ad6a6863f6a919f))

## [0.105.2](https://github.com/holgerleichsenring/agent-smith/compare/v0.105.1...v0.105.2) (2026-07-10)


### Bug Fixes

* **context-yaml:** preserve arch/quality values + require stack.image ([3deb28b](https://github.com/holgerleichsenring/agent-smith/commit/3deb28b76d64300f63767490afb8956128d804bf))
* **context-yaml:** preserve arch/quality values + require stack.image on write ([e58a443](https://github.com/holgerleichsenring/agent-smith/commit/e58a443fc3bcafe1e475b7844f757eae450ac994))
* **feature:** pipeline_name add-feature accepted end-to-end (drop feature-implementation) ([cb96184](https://github.com/holgerleichsenring/agent-smith/commit/cb961844f6020205106e75fbe7501a1ed5330b6b))

## [0.105.1](https://github.com/holgerleichsenring/agent-smith/compare/v0.105.0...v0.105.1) (2026-07-09)


### Bug Fixes

* **api-scan:** publish a synthetic Repository in passive mode ([9cb39d8](https://github.com/holgerleichsenring/agent-smith/commit/9cb39d82beec50a11ed0f3ec7ad0ffea42535e91))
* **api-scan:** publish a synthetic Repository in passive mode ([169cceb](https://github.com/holgerleichsenring/agent-smith/commit/169ccebbe27e4a49e44450c74efb9650a0a010fc))
* **docker:** copy phase-spec.schema.json into CLI + Server build contexts ([a453697](https://github.com/holgerleichsenring/agent-smith/commit/a45369791e582d2eaa30aa7fcce3a33127cd3fd1))
* **docker:** copy phase-spec.schema.json into CLI + Server build contexts ([46474c3](https://github.com/holgerleichsenring/agent-smith/commit/46474c3897babfc402859fe95b02082753d10efa))

## [0.105.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.104.2...v0.105.0) (2026-07-09)


### Features

* /create-phase files confirmed outcomes as tracker tickets (p0315c) ([d5719fc](https://github.com/holgerleichsenring/agent-smith/commit/d5719fcd97dfa42b7ac36f95590bde5b0e5cb67b))
* conversational spec dialog — chat-first design & delivery partner (p0315a-f) ([5bb111d](https://github.com/holgerleichsenring/agent-smith/commit/5bb111d69498ea92315b398667e7aecef5663401))
* design-partner-master with tiered code grounding (p0315b) ([f0a2f0d](https://github.com/holgerleichsenring/agent-smith/commit/f0a2f0d35a79a86cbad9d5b3c45f63ae9481a9a6))
* phase-execution pipeline runs spec-first from phase tickets (p0315d) ([1c65421](https://github.com/holgerleichsenring/agent-smith/commit/1c65421f9c881382834059e30927786c7ffff2d7))
* pr-review — agent-smith performs PR reviews (p0167a-c) ([d339aa6](https://github.com/holgerleichsenring/agent-smith/commit/d339aa61dc52925a09b969ab5810c3feacaccb48))
* pr-review findings compiled + posted as idempotent PR comments (p0167c) ([e761bf4](https://github.com/holgerleichsenring/agent-smith/commit/e761bf480d8b9023f1992fe6c25a26d48f1e2a60))
* pr-review preset, pr-event webhooks + structured diff analysis (p0167a) ([6b3607f](https://github.com/holgerleichsenring/agent-smith/commit/6b3607f22883de47e3e90f9ba562cc9b862648a4))
* pr-review skill roster + line-range observations (p0167b) ([d3263a7](https://github.com/holgerleichsenring/agent-smith/commit/d3263a7b734d3ccc24252ee6af50d70fe3f92461))
* real ticket creation across all four tracker providers (p0315f) ([1f37eb5](https://github.com/holgerleichsenring/agent-smith/commit/1f37eb5942b9b7ccfbdde9a756964074b0324341))
* spec-dialog session with per-thread transcripts + active scope (p0315a) ([4d3ddcb](https://github.com/holgerleichsenring/agent-smith/commit/4d3ddcb5dc9bb61ada7ddfed99d7bbe5fd541d83))
* ticket conversation + attachments reach the master (p0317) ([f1224a7](https://github.com/holgerleichsenring/agent-smith/commit/f1224a74898204c8fa9f1e0de465e72948a41e54))
* ticket conversation + attachments reach the master (p0317) ([1d455c7](https://github.com/holgerleichsenring/agent-smith/commit/1d455c79a3f389efa4876003603156f0350ba3d8))
* typed outcome resolution + epic decomposition for spec dialogs (p0315e) ([f36c68b](https://github.com/holgerleichsenring/agent-smith/commit/f36c68b514f1cc2028b9cf078f9398157ebca516))


### Bug Fixes

* **bootstrap:** init-project never wrote context.yaml (p0193 regression) ([8203190](https://github.com/holgerleichsenring/agent-smith/commit/8203190c19585a14f1ac6625596ab0b84f0f9394))
* **bootstrap:** init-project never wrote context.yaml (p0193 regression) ([c7efaed](https://github.com/holgerleichsenring/agent-smith/commit/c7efaed551afb58d83569ba66cf701b3c46935ae))
* docker-tier harness — git config + GeneratePlan seed alignment (p0281); plan p0282 ([1e51d4b](https://github.com/holgerleichsenring/agent-smith/commit/1e51d4b016c147c51447c456a017bb17cb9eef0b))

## [0.104.2](https://github.com/holgerleichsenring/agent-smith/compare/v0.104.1...v0.104.2) (2026-07-06)


### Bug Fixes

* **dashboard:** run header showed sandbox keys as repos ([a5a7b64](https://github.com/holgerleichsenring/agent-smith/commit/a5a7b64b38fc3930f4b821725de54ea67889b8d8))
* **dashboard:** run header showed sandbox keys as repos (5 badges for 3 repos) ([e13a0a3](https://github.com/holgerleichsenring/agent-smith/commit/e13a0a372d06f157ef33c71a43ca451609597bd5))


### Performance Improvements

* don't eager-resolve the whole DI graph on startup (ValidateOnBuild) ([ea6b13d](https://github.com/holgerleichsenring/agent-smith/commit/ea6b13d020aa4fc8fd68a4d17103d138d76152cb))
* faster startup — stop eager-resolving the whole DI graph (ValidateOnBuild) ([4208da1](https://github.com/holgerleichsenring/agent-smith/commit/4208da1ce220f9102c48d377028f88e6f99c9dc6))

## [0.104.1](https://github.com/holgerleichsenring/agent-smith/compare/v0.104.0...v0.104.1) (2026-07-06)


### Bug Fixes

* AzDO discovery no longer requires agent-smith tag on every ticket (p0300c regression) ([5e55a29](https://github.com/holgerleichsenring/agent-smith/commit/5e55a2956cd57989260dcfc6cfe38a5444a94330))
* AzDO discovery no longer requires an agent-smith tag on every ticket (p0300c regression) ([faa088e](https://github.com/holgerleichsenring/agent-smith/commit/faa088ecbea01e0a230db3974e3db91aa134b644))

## [0.104.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.103.0...v0.104.0) (2026-07-06)


### Features

* capacity-aware run admission — sequential when resources are full (p0269a, p0269b) ([c9a4198](https://github.com/holgerleichsenring/agent-smith/commit/c9a4198fbe8c807f7dbbb20360649a97c44fe701))
* capacity-aware run admission — sequential when resources are full (p0269a, p0269b) ([7fcf723](https://github.com/holgerleichsenring/agent-smith/commit/7fcf7236e84c49a9df001b9e5cdfbeaffe7f6da6))
* poller/discovery + run-lifecycle hardening (p0300c) ([0b34cca](https://github.com/holgerleichsenring/agent-smith/commit/0b34ccaa8bace3e75320dcc90df962e7d3382916))
* poller/discovery + run-lifecycle hardening (p0300c) ([800cec4](https://github.com/holgerleichsenring/agent-smith/commit/800cec4412d52eaf64979d7fd0cf8d8041ecafee))
* ticket comprehension + clarification gate (p0318) ([195ad3a](https://github.com/holgerleichsenring/agent-smith/commit/195ad3a6cffd3b40084259767a43524c59e877c6))
* ticket comprehension + clarification gate (p0318) ([281a633](https://github.com/holgerleichsenring/agent-smith/commit/281a633b8625b6212d31fd29711c3dab065f8954))
* ticket-instruction contract (p0316) + retire base p0312 ([d9d87b5](https://github.com/holgerleichsenring/agent-smith/commit/d9d87b532fbc512b9496723fc92275a9a1d6375f))
* ticket-instruction untrusted-content boundary — backend (p0316) ([d378477](https://github.com/holgerleichsenring/agent-smith/commit/d3784778517977cca047c659cb50c20596ec5997))


### Bug Fixes

* **dashboard:** handle TicketInstructionIgnored in exhaustive event switches (p0316) ([788ecd7](https://github.com/holgerleichsenring/agent-smith/commit/788ecd77dd50fec02928a14f81bb6c34a46c1d0a))
* **dashboard:** mirror TicketInstructionIgnoredEvent in hub-events.ts (p0316) ([338fe97](https://github.com/holgerleichsenring/agent-smith/commit/338fe971ae66e4f1feac497d0e3d503a5e590cdc))
* P0318 ticket comprehension and clarification gate ([12d14cf](https://github.com/holgerleichsenring/agent-smith/commit/12d14cf61916e387e7bedb4c5337a17d18cae6d7))

## [0.103.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.102.0...v0.103.0) (2026-07-06)


### Features

* consolidate multi-sandbox commits into one PR (p0299) ([f8125e4](https://github.com/holgerleichsenring/agent-smith/commit/f8125e483432fb369c64b9d61a23e128ec46bf6c))
* consolidate multi-sandbox commits into one PR (p0299) ([ec3a6f0](https://github.com/holgerleichsenring/agent-smith/commit/ec3a6f0f3cdfbca21bff05fb375beb7b8b375c27))
* Jira native transitions + scan hardening + skills guard (p0300a, p0300b, p0313) ([1fd9cd7](https://github.com/holgerleichsenring/agent-smith/commit/1fd9cd72340d4bc038b921f81c32384951a2924f))
* Jira native transitions + scan hardening + skills guard (p0300a, p0300b, p0313) ([b9613bd](https://github.com/holgerleichsenring/agent-smith/commit/b9613bd8d3cea4150a10bbe5db43cbe259242713))

## [0.102.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.101.0...v0.102.0) (2026-07-05)


### Features

* idempotent CreatePullRequestAsync for GitLab + GitHub (p0298) ([2576aad](https://github.com/holgerleichsenring/agent-smith/commit/2576aada7606d6d99e9875a443f9ab45721b79e3))

## [0.101.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.100.0...v0.101.0) (2026-07-05)


### Features

* robust LLM-JSON responses for analyzer + discovery (p0294) ([5c6e679](https://github.com/holgerleichsenring/agent-smith/commit/5c6e67961747f06a0a6601424a75b0ee6b1dec31))
* self-contained GitLab addressing + quiet benign DB cancellations (p0296) ([ed28194](https://github.com/holgerleichsenring/agent-smith/commit/ed28194a0c2daac70ecde39b7f4ae036b73336ef))


### Bug Fixes

* GitLab project-id double-encoding broke nested-subgroup context discovery (p0297) ([11bf5d2](https://github.com/holgerleichsenring/agent-smith/commit/11bf5d2975028ac3809d995da845675f924ed09f))

## [0.100.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.99.0...v0.100.0) (2026-07-02)


### Features

* connectivity probes — agents, sandbox, redis, persistence, chat (p0293) ([4c62b68](https://github.com/holgerleichsenring/agent-smith/commit/4c62b6826365dab6fe7853b55891cd429e91227c))
* connectivity probes for agents, sandbox, redis, persistence, chat (p0293) ([416e8c3](https://github.com/holgerleichsenring/agent-smith/commit/416e8c32cc71cc591a0ee1751f7f3184d7450e26))

## [0.99.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.98.0...v0.99.0) (2026-07-02)


### Features

* connection diagnostics — active probe + webhook panel (p0292) ([ed51188](https://github.com/holgerleichsenring/agent-smith/commit/ed5118886c1c01c4e84765e120819489f0ecf814))
* connection diagnostics — active probe + webhook panel on the dashboard (p0292) ([1ee2a0c](https://github.com/holgerleichsenring/agent-smith/commit/1ee2a0cdd3056a1849c9601113960fd4ba295ab9))
* static (discovery-free) exact connection repo refs (p0285) ([f8d8cad](https://github.com/holgerleichsenring/agent-smith/commit/f8d8cad0611ed2eef4c3557625e18db888d20b99))
* static (discovery-free) exact connection repo refs (p0285) ([263abca](https://github.com/holgerleichsenring/agent-smith/commit/263abca708f57cbb91d37e8b114085de0b1fe74d))

## [0.98.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.97.1...v0.98.0) (2026-06-25)


### Features

* composed ticket-discovery query — fetch only claimable candidates (p0283a/b) ([5f9d027](https://github.com/holgerleichsenring/agent-smith/commit/5f9d027d4c63f3a3245af2024688ad1e0f1be72e))
* composed ticket-discovery query + scaling/bugfixes (p0283a/b) ([49e3795](https://github.com/holgerleichsenring/agent-smith/commit/49e37959c1268db783c4f7a4c69efe8761b679e0))
* config-catalog simplification — connections/discovery, tracker-owns-workflow, deployment pin, projectless CLI (p0281a–d) ([db3a9ef](https://github.com/holgerleichsenring/agent-smith/commit/db3a9ef4773fe081206c3b408213b63a16d3ed59))
* connections catalog + out-of-band repo discovery with glob (p0281a) ([1a04f22](https://github.com/holgerleichsenring/agent-smith/commit/1a04f22420a262922374d28458942d8889f9cb54))
* projectless CLI scans via --agent (p0281d) ([d8d9978](https://github.com/holgerleichsenring/agent-smith/commit/d8d9978aace3ca575890be055cc51c1116245cf4))
* push the resolution tag in ALL providers + log cleanup (p0283b) ([c7bc24d](https://github.com/holgerleichsenring/agent-smith/commit/c7bc24d2d7b64bb1c7d22e81a48262c7b3d9c57a))
* single deployment image pin for orchestrator + sandbox (p0281c) ([d808462](https://github.com/holgerleichsenring/agent-smith/commit/d808462917a6568d2f91267886ed52d32cf66f14))
* tracker owns the trigger workflow + flat resolution shorthand (p0281b) ([d0836b4](https://github.com/holgerleichsenring/agent-smith/commit/d0836b4353974e0d0279e094e47013cdb4be504c))

## [0.97.1](https://github.com/holgerleichsenring/agent-smith/compare/v0.97.0...v0.97.1) (2026-06-24)


### Bug Fixes

* replay run rail from real-time Redis (minus stdout), not the batched DB (p0291) ([92829fc](https://github.com/holgerleichsenring/agent-smith/commit/92829fc731928f527d5634b80b733dd76bd3b089))
* replay the run rail from the real-time Redis stream (minus stdout), not the batched DB ([2dd49f4](https://github.com/holgerleichsenring/agent-smith/commit/2dd49f4905e1f6be61294b7649c42549609feca6))

## [0.97.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.96.2...v0.97.0) (2026-06-24)


### Features

* make all Jira REST endpoints operator-overridable from config ([2372568](https://github.com/holgerleichsenring/agent-smith/commit/2372568b394419854416bc181fd33380b627cedb))
* make all Jira REST endpoints operator-overridable from config (p0284) ([c5d60a6](https://github.com/holgerleichsenring/agent-smith/commit/c5d60a6a0ddf8bb60093ac5836d5aef576b7b0cc))


### Bug Fixes

* implement JiraTicketProvider.ListOpenAsync so polling discovers fresh tickets ([e8527cc](https://github.com/holgerleichsenring/agent-smith/commit/e8527cc5d43830c2efc8b8e6d8118b1f3209abdb))
* implement ListOpenAsync for GitHub + GitLab providers too ([5ac6550](https://github.com/holgerleichsenring/agent-smith/commit/5ac6550b6724ea884cbf2eef46b7834e7d6cda87))
* ListOpenAsync across all ticket providers + remove dead ListByLabelsInOpenStatesAsync (p0285) ([92bae7d](https://github.com/holgerleichsenring/agent-smith/commit/92bae7d945e116c08edd6abc3fdf857b39544dda))
* render agent-smith's HTML comment subset as real ADF on Jira ([c7953b7](https://github.com/holgerleichsenring/agent-smith/commit/c7953b74e15a3bfae52386ecedbaccf6374a0e4e))
* render agent-smith's HTML comment subset as real ADF on Jira (p0290) ([0934a74](https://github.com/holgerleichsenring/agent-smith/commit/0934a740db179856e8e857642e314c6b1f1c2179))
* replay run execution from durable DB trail when Redis stream is gone (p0286) ([e2afb62](https://github.com/holgerleichsenring/agent-smith/commit/e2afb628ed5b6f9499cc7d214e85ab9e7767ebb4))
* replay run execution from the durable DB trail when the Redis stream is gone ([9c91aa3](https://github.com/holgerleichsenring/agent-smith/commit/9c91aa3e74d8bca3b4f0c2e5ba2da06a6e260705))
* source run execution rail from the durable DB trail, not the Redis replay (p0288) ([942d86e](https://github.com/holgerleichsenring/agent-smith/commit/942d86ebc3a160317e1a943440a092e6f2d541d2))
* source the run execution rail from the durable DB trail, not the Redis replay ([6eddbaa](https://github.com/holgerleichsenring/agent-smith/commit/6eddbaa7731b035b914a846f650beab48defc166))

## [0.96.2](https://github.com/holgerleichsenring/agent-smith/compare/v0.96.1...v0.96.2) (2026-06-22)


### Bug Fixes

* migrate Jira search to /rest/api/3/search/jql (410 Gone) ([ab75721](https://github.com/holgerleichsenring/agent-smith/commit/ab757216c89a0bebeeac78bfe54d6331c85b9787))
* migrate Jira search to /rest/api/3/search/jql (410 Gone) ([1e083d9](https://github.com/holgerleichsenring/agent-smith/commit/1e083d96797cc48a40cbd4b3f292f64e90942dcf))

## [0.96.1](https://github.com/holgerleichsenring/agent-smith/compare/v0.96.0...v0.96.1) (2026-06-22)


### Bug Fixes

* surface ticket-provider HTTP errors instead of swallowing to null (p0283) ([a6437ce](https://github.com/holgerleichsenring/agent-smith/commit/a6437ceea0b32beae515883396de482517befeea))
* surface ticket-provider HTTP errors instead of swallowing to null (p0283) ([e70006d](https://github.com/holgerleichsenring/agent-smith/commit/e70006d051866867577cd3ee257df0e28c0bf28f))

## [0.96.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.95.0...v0.96.0) (2026-06-22)


### Features

* scan coverage re-drive + honest source anchoring (p0279) ([5909780](https://github.com/holgerleichsenring/agent-smith/commit/5909780d83392abd406978b920e7766cc9bd80a3))
* scan coverage re-drive + honest source anchoring (p0279) ([066ee98](https://github.com/holgerleichsenring/agent-smith/commit/066ee98da2da361050ed5c0ae989442b14838371))

## [0.95.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.94.0...v0.95.0) (2026-06-21)


### Features

* feed and reframe the scan masters so they actually review (p0278) ([664f0fe](https://github.com/holgerleichsenring/agent-smith/commit/664f0fe40ff79c99fff1e9af97febacc0cb56fa2))
* feed and reframe the scan masters so they actually review (p0278) ([202ad33](https://github.com/holgerleichsenring/agent-smith/commit/202ad3301ed1f9c8dc7db03a1f48a1c917c59c31))

## [0.94.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.93.0...v0.94.0) (2026-06-21)


### Features

* deliver api-security master findings (p0267) ([c8e9501](https://github.com/holgerleichsenring/agent-smith/commit/c8e950164b047c20b634227fab8b46806c80edba))
* deliver api-security master findings (p0267) ([96a4045](https://github.com/holgerleichsenring/agent-smith/commit/96a4045241364af29effd7ecf015fd885328de32))
* deliver security-scan master triage with a safety net (p0277) ([ebf64bf](https://github.com/holgerleichsenring/agent-smith/commit/ebf64bfa84b87f7e59216b5263b588287731b890))
* deliver security-scan master triage with a safety net (p0277) ([d21e56e](https://github.com/holgerleichsenring/agent-smith/commit/d21e56ed9dcc4631de90a3ca518e853c18ff2318))


### Bug Fixes

* GeneratePlan decision write is best-effort on sandbox repos (p0276b) ([4ae81fa](https://github.com/holgerleichsenring/agent-smith/commit/4ae81faa871e33b391225ea4242a897d68e65ee1))

## [0.93.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.92.0...v0.93.0) (2026-06-19)


### Features

* config-aware live LLM pricing (p0274) ([80f9c74](https://github.com/holgerleichsenring/agent-smith/commit/80f9c740ba2fe380bd82551cd5207b58193926ac))
* plan before execution (p0276) ([fd714d4](https://github.com/holgerleichsenring/agent-smith/commit/fd714d4aedfd4ac9826024eb97defd8d5f1289ce))
* regression-aware verification + durable-on-red (p0273) ([5116354](https://github.com/holgerleichsenring/agent-smith/commit/5116354b71f30aa2febc338cdbf38f9b34ebaa3d))
* run-detail step skeleton from pipeline structure (p0275) ([04d048f](https://github.com/holgerleichsenring/agent-smith/commit/04d048f704826cd9ff52dada44426815d72eab25))
* run-quality batch — cost, verification, run-detail IA, plan-before-exec (p0273-p0276) ([34c9f6f](https://github.com/holgerleichsenring/agent-smith/commit/34c9f6f0ef03d42073d58c4c9c60c3933a150174))

## [0.92.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.91.0...v0.92.0) (2026-06-18)


### Features

* per-language toolchain image config (p0245) ([f70fd82](https://github.com/holgerleichsenring/agent-smith/commit/f70fd82bd4b5753984f951c77f1aa7a20c620d6a))
* per-stack sandbox sizing from context.yaml stack.resources (p0268) ([f545508](https://github.com/holgerleichsenring/agent-smith/commit/f545508cdc9192d193577638f0c8db659f385ffe))
* per-stack sandbox sizing from context.yaml stack.resources (p0268) ([b5db95c](https://github.com/holgerleichsenring/agent-smith/commit/b5db95c518145034e13f42646e440ba52d718324))
* sandbox secret injection (p0272) + per-language toolchain images (p0245) ([a8593d2](https://github.com/holgerleichsenring/agent-smith/commit/a8593d25b0952216199d45e8dd86236a1ea5824a))
* sandbox secret injection for k8s pods (p0272) ([bdee7bc](https://github.com/holgerleichsenring/agent-smith/commit/bdee7bc7244586786442015820a5c48be1278004))

## [0.91.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.90.0...v0.91.0) (2026-06-15)


### Features

* **p0261:** --context flag to pin a scan to one named context ([c17175c](https://github.com/holgerleichsenring/agent-smith/commit/c17175c5350d3372946cf0cfca1d6feb333d9895))
* **p0261:** --context scan flag + fix release Docker cache race ([5b3b77d](https://github.com/holgerleichsenring/agent-smith/commit/5b3b77de02cc32df5482e25ac0f81a2e525266b1))
* **p0266:** dashboard config explorer — redacted /api/config + System→Config graph ([aefa4ec](https://github.com/holgerleichsenring/agent-smith/commit/aefa4ec1b1ef7f7d65fc09422eb011625b6c798b))
* **p0266:** dashboard config explorer — redacted /api/config + System→Config graph ([6db4085](https://github.com/holgerleichsenring/agent-smith/commit/6db4085d1ec9d3d8c20ea61f8cc44b552c7bb937))
* **p0270a:** materialize resolved config — one resolver for run path + dashboard ([0ad9851](https://github.com/holgerleichsenring/agent-smith/commit/0ad985195ccfe8d2be963d3a390e8c39114c401d))
* **p0270b:** config explorer → explainer — drill-in, provenance, tracker roles ([d9e7af0](https://github.com/holgerleichsenring/agent-smith/commit/d9e7af0fe96443301e91315d6e9d1d153cdb7c2d))
* **p0270:** materialized resolved config + config explainer dashboard ([8ced7a3](https://github.com/holgerleichsenring/agent-smith/commit/8ced7a387d4703e33b2163f264634b4d57fd3d2c))
* **p0271:** config detail sheet — project combobox replaces the topology graph ([ac737d9](https://github.com/holgerleichsenring/agent-smith/commit/ac737d927d1edb5d08b1ac70547156cfc21791f2))
* **p0271:** config detail sheet — project combobox replaces the topology graph ([baebe45](https://github.com/holgerleichsenring/agent-smith/commit/baebe45eac023f19e0192269716b4a7026fb8891))


### Bug Fixes

* **p0269:** terminalize native ticket status on a thrown fatal failure ([4f55e58](https://github.com/holgerleichsenring/agent-smith/commit/4f55e58779e5fc03bf6f7cb343a062b5abbd81ac))

## [0.90.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.89.0...v0.90.0) (2026-06-09)


### Features

* failed-status terminalization + tags non-decisive for triggering (p0261) ([7ded8f8](https://github.com/holgerleichsenring/agent-smith/commit/7ded8f882c09d416656a07adfce4883c3a75a19b))
* **p0258:** configurable master fix-iteration budget ({MaxFixIterations}) ([df56461](https://github.com/holgerleichsenring/agent-smith/commit/df564612cad34217b9b177e979a918d21dbac275))
* **p0258:** dedicated Plan node in the run detail ([#5](https://github.com/holgerleichsenring/agent-smith/issues/5)) ([c465c8a](https://github.com/holgerleichsenring/agent-smith/commit/c465c8a662bef400c0832ae636ab72ba53d10b3f))
* **p0259:** cancel-ux terminal status + persisted canceling state ([582dcf1](https://github.com/holgerleichsenring/agent-smith/commit/582dcf16a0c313dfc7f5aa6d4d94b2e5500dbe00))
* **p0260:** audit every outbound ticket write with its agent-smith caller ([342bc60](https://github.com/holgerleichsenring/agent-smith/commit/342bc60d6a21de24cfefacecb13e7cdbeb941137))
* **p0265:** LLM-named toolchain image via context.yaml stack.image ([fa9ca83](https://github.com/holgerleichsenring/agent-smith/commit/fa9ca8369d4aefd8b1ef7eb00d848d99a81af0c2))
* status recognizer — derive lifecycle from native status + lease, drop TicketLifecycles (p0262) ([ef10773](https://github.com/holgerleichsenring/agent-smith/commit/ef107731741a0b7fc44b679c7a1c605c4cb42586))
* verdict-nudge — re-prompt once for a missing Phase 4 verdict (p0263) ([75389a0](https://github.com/holgerleichsenring/agent-smith/commit/75389a0ab2c9ca30d8ece8164faea4240c8801ba))


### Bug Fixes

* P0258 run lifecycle fixes ([c9c3861](https://github.com/holgerleichsenring/agent-smith/commit/c9c386186548a52e76bb8e5ce293e99329ca2dfc))
* **p0258:** capture Run-step stdout into StepResult.OutputContent — fixes commit/PR ([83e3c6c](https://github.com/holgerleichsenring/agent-smith/commit/83e3c6c64d41ffce8d57a6f61fcf34cd0f43fa29))
* **p0258:** drop PersistWorkBranch from happy path — restores commit + PR ([a1b873c](https://github.com/holgerleichsenring/agent-smith/commit/a1b873c7c3deb670a22d5679eb7720fa733bb23a))
* **p0258:** label projection re-anchors on actual state — stop re-trigger loop ([f79aea8](https://github.com/holgerleichsenring/agent-smith/commit/f79aea8fc65902c62da3da37d46051d41990e8af))
* **p0258:** re-runnable tickets, run-id-only record dir, quiet EF logs ([8f6994c](https://github.com/holgerleichsenring/agent-smith/commit/8f6994c8b7e8f01e806639080566dd8cb41d58b8))
* **p0258:** run lifecycle — re-runnable tickets, no re-trigger loop, no cold-start replay ([ba8633e](https://github.com/holgerleichsenring/agent-smith/commit/ba8633eac6c0789cc38a38ad1de7e50ef39777b9))
* **p0258:** stop cold-start run replay, heartbeat-authoritative lease ([34e14fd](https://github.com/holgerleichsenring/agent-smith/commit/34e14fddf28a041e02d254ada2f44c82caad192c))
* **p0260:** gate StaleJobDetector revert on DB-authoritative status ([744ee14](https://github.com/holgerleichsenring/agent-smith/commit/744ee14a3031c7576c9004b445f10d4be5883440))
* **p0260:** soft tool-errors for repo routing + finish CLI graph hardening ([6c903da](https://github.com/holgerleichsenring/agent-smith/commit/6c903da3c109d277eb074f624ecc71e83691523b))
* **p0264:** system-stream UI polish — untangle timestamp/tag, live freshness, bigger rows ([7d75130](https://github.com/holgerleichsenring/agent-smith/commit/7d75130bc641df95ddc14e6044765a121fd639fa))
* **test:** correct FixBug harness path predicates for prefix-stripped writes ([82ecb6d](https://github.com/holgerleichsenring/agent-smith/commit/82ecb6d14b06f08a272395fdc4e904729cefd68f))
* widen CommandTimeline verb column so long tool names don't overlap the path ([fb03d4f](https://github.com/holgerleichsenring/agent-smith/commit/fb03d4f27d2b62ba490ef621b2112f850cfc94f9))

## [0.89.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.88.1...v0.89.0) (2026-06-08)


### Features

* DB-authoritative claim — drop redundant Redis heartbeat gate (p0251) ([db2dc1f](https://github.com/holgerleichsenring/agent-smith/commit/db2dc1f9fbb44014948d94ee23161289ae6b6802))
* **p0253:** result.md fidelity — result: reflects the verdict, .agentsmith excluded (partial) ([a3afa27](https://github.com/holgerleichsenring/agent-smith/commit/a3afa278219275e41375485c6156405dbc380614))
* **p0254:** close session loose ends — tracker SignalR fix + de-flake parallel tests ([2925478](https://github.com/holgerleichsenring/agent-smith/commit/292547834f18ec76ba6aac1e82adfac2dd26de6d))
* **p0255:** drive plan-&gt;apply when the master plans but edits no source ([704e313](https://github.com/holgerleichsenring/agent-smith/commit/704e31324bfb3831d9a9c338201479943e0e6421))
* **p0256:** diagnose the empty record-PR stage (instrument, don't guess) ([e02ef87](https://github.com/holgerleichsenring/agent-smith/commit/e02ef87f21dd297f1aa24db4501601af7c90c414))
* retire the Redis job heartbeat — DB lease is the sole liveness source (p0252) ([253412b](https://github.com/holgerleichsenring/agent-smith/commit/253412b1a773c6f5b134d675fa2f6c68b9cee774))


### Bug Fixes

* multi-group repo source change dropped at commit (p0249) ([fee10d7](https://github.com/holgerleichsenring/agent-smith/commit/fee10d7822be9bf14c7e9e61a5d37e912dfeb74b))
* **p0257:** sandbox idle-timeout 5min-&gt;30min — stop idle sandboxes self-killing during sequential analyze ([bfb6263](https://github.com/holgerleichsenring/agent-smith/commit/bfb6263d66c2cb60bd7999d6f7f0f074133172f2))
* run correctness and harness ([dee7452](https://github.com/holgerleichsenring/agent-smith/commit/dee74524968e1cafb0c13f7e35658ab813306f7f))
* unify sandbox addressing onto repo name (p0250) ([34fe30b](https://github.com/holgerleichsenring/agent-smith/commit/34fe30bc837b8e884786a626a89572fd97e36b46))

## [0.88.1](https://github.com/holgerleichsenring/agent-smith/compare/v0.88.0...v0.88.1) (2026-06-06)


### Bug Fixes

* clear stale terminal lifecycle tag on a completed re-run (p0237) ([f490932](https://github.com/holgerleichsenring/agent-smith/commit/f49093274466476b4d0b44fd3d155029e8765ec8))
* log sandbox exit evidence (exitCode + OOMKilled + error) on vanish (p0237) ([27b022b](https://github.com/holgerleichsenring/agent-smith/commit/27b022babce2cc953d32bbac382fdde4066ca79e))
* raise sandbox memory default 2Gi-&gt;4Gi (OOM root cause) + harden no-empty-catch principle (p0237) ([22c43a6](https://github.com/holgerleichsenring/agent-smith/commit/22c43a6edd2d5c0fc50bbe58ddec9a9a22b38fbf))
* surface sandbox-vanished cause + survive missing CodeChanges + log silent shutdown-cancel (p0237) ([9115ce1](https://github.com/holgerleichsenring/agent-smith/commit/9115ce15ddafb5804f8bf2dcbe1890fb49340fb6))

## [0.88.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.87.0...v0.88.0) (2026-06-05)


### Features

* always-finalize failed runs + plan.md in run dir + skill hardening (p0237) ([33ce8c7](https://github.com/holgerleichsenring/agent-smith/commit/33ce8c794d3cdcb423db78f7f739647f0b6c2931))


### Bug Fixes

* make internal LLM-timeout cancel self-explanatory + 500k per-call token headroom (p0236) ([74830d0](https://github.com/holgerleichsenring/agent-smith/commit/74830d0c1dc4b9c95d308ef2992cace051ba9450))

## [0.87.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.86.0...v0.87.0) (2026-06-05)


### Features

* LLM network-timeout fix + result-view markdown/plan.md + dashboard publish (p0235) ([5e9bc7f](https://github.com/holgerleichsenring/agent-smith/commit/5e9bc7fdc67e8ee774ffc5ecdf7047701a7c470a))


### Bug Fixes

* **dashboard:** lift event-stream meta 11→12px + tokenize stray text-xs (p0235) ([d6703cc](https://github.com/holgerleichsenring/agent-smith/commit/d6703cc056635161be85a0b88cb3c4db6fc0de1c))

## [0.86.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.85.0...v0.86.0) (2026-06-05)


### Features

* per-repo run-record + always-PR, and publish the dashboard image (p0234) ([08b59ae](https://github.com/holgerleichsenring/agent-smith/commit/08b59ae955ab2320fa3c268a8318d65826cc2f86))
* per-repo run-record + always-PR, and publish the dashboard image (p0234) ([ad95320](https://github.com/holgerleichsenring/agent-smith/commit/ad95320ab183af4401fd9f116e13b16e84b5e22b))
* surface cancellation reasons + live-watching polish (p0232) ([695947e](https://github.com/holgerleichsenring/agent-smith/commit/695947e9c110a415bc4ab9cbf1495654ea601ba8))
* surface cancellation reasons + live-watching polish (p0232) ([7eaf3cd](https://github.com/holgerleichsenring/agent-smith/commit/7eaf3cdea455dbb2a6502439b6830b8fee14d57e))


### Bug Fixes

* Runs list updates live on a new job (p0233) ([8194e9d](https://github.com/holgerleichsenring/agent-smith/commit/8194e9d44984596e16d0e6c658df842705b81342))
* Runs list updates live on a new job (p0233) ([0c9ad6b](https://github.com/holgerleichsenring/agent-smith/commit/0c9ad6b2bb424cecb21d2505e80f4cde44ef6a4f))

## [0.85.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.84.0...v0.85.0) (2026-06-05)


### Features

* command timeline below repos + per-repo distinction (p0229) ([6b0ff01](https://github.com/holgerleichsenring/agent-smith/commit/6b0ff01ea0aeaa9de0a9534bf02c3f5e913d286f))
* command timeline below repos + per-repo distinction (p0229) ([7987b07](https://github.com/holgerleichsenring/agent-smith/commit/7987b0779bb82816f70f0fe6ff57ef6fc0667d97))
* configurable per-project sandbox timeouts (p0230) ([3bc5208](https://github.com/holgerleichsenring/agent-smith/commit/3bc520834b0ce2710750fcd22089a137987d27f3))
* configurable per-project sandbox timeouts (p0230) ([6b7f4f7](https://github.com/holgerleichsenring/agent-smith/commit/6b7f4f7d9915670e7b914ae6ca4fb739a774ee20))
* unified execution timeline + k8s dashboard manifests (p0231) ([7e60487](https://github.com/holgerleichsenring/agent-smith/commit/7e604879cc70a65cc4c1f814f076446ef611962c))
* unified execution timeline + k8s dashboard manifests (p0231) ([b0348dd](https://github.com/holgerleichsenring/agent-smith/commit/b0348dd5e5bf4854889372253ffbf1d82103a5b1))


### Bug Fixes

* scrub customer fingerprints from redesign mockup HTML ([ffc2de4](https://github.com/holgerleichsenring/agent-smith/commit/ffc2de44b95a3cd42b51d1773bec7a6b68201984))
* scrub customer fingerprints from redesign mockup HTML ([c094529](https://github.com/holgerleichsenring/agent-smith/commit/c0945291ddf1f0e86003bc719eaa1d178f3f9204))

## [0.84.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.83.0...v0.84.0) (2026-06-05)


### Features

* run-detail transparency + readability (p0228) ([f8213be](https://github.com/holgerleichsenring/agent-smith/commit/f8213bea728d972e9fc48a85f18e707a734c7464))
* run-detail transparency + readability (p0228) ([4183655](https://github.com/holgerleichsenring/agent-smith/commit/4183655cc8abd747c79fb92686077f7d34172127))

## [0.83.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.82.0...v0.83.0) (2026-06-04)


### Features

* run-detail live-watching + false-alarm cleanup (p0227) ([b4a2bc4](https://github.com/holgerleichsenring/agent-smith/commit/b4a2bc42636baaf6b404d5f88983efb75286efbb))
* run-detail live-watching + false-alarm cleanup (p0227) ([f4a2982](https://github.com/holgerleichsenring/agent-smith/commit/f4a2982fa2a5f3ae19c3e9c6592a450fac8e8629))


### Bug Fixes

* PersistWorkBranch only persists repos that have changes (p0226) ([f4018b4](https://github.com/holgerleichsenring/agent-smith/commit/f4018b4608feb0b988b7ccda8e56bbefb4ab2423))
* PersistWorkBranch only persists repos that have changes (p0226) ([df5eee8](https://github.com/holgerleichsenring/agent-smith/commit/df5eee88e5aa37cd1c691810e92f64f3fab3b819))
* Runs list shows immediately + recent runs keep their metadata (p0225) ([d402711](https://github.com/holgerleichsenring/agent-smith/commit/d402711998979bd59761236920de41ee40fa3901))
* Runs list shows immediately + recent runs keep their metadata (p0225) ([e3777c4](https://github.com/holgerleichsenring/agent-smith/commit/e3777c467fd13bf277a2493a3c0db3c6a2dd7854))

## [0.82.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.81.0...v0.82.0) (2026-06-04)


### Features

* meaningful commit/PR-step outcomes (p0223) ([913b5ad](https://github.com/holgerleichsenring/agent-smith/commit/913b5ad397888fb4548cc248474c47fe94ece9b1))
* operator-only prerequisites + honor context.yaml; real model on in-flight rows (p0224) ([e235339](https://github.com/holgerleichsenring/agent-smith/commit/e235339536a508c40f58bff225e56fca9759ec54))


### Bug Fixes

* trust LLM for deps + honor context.yaml; real model on in-flight rows (p0224, +p0223) ([8b8b269](https://github.com/holgerleichsenring/agent-smith/commit/8b8b2697fef336d26a34eb046ba00d8f2c9f0fdf))

## [0.81.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.80.0...v0.81.0) (2026-06-04)


### Features

* catalog contents browser + lazy catalog-contents API (p0221) ([4c134e7](https://github.com/holgerleichsenring/agent-smith/commit/4c134e714a283ebf41c66cb4493bfe86e6ffe5e6))
* dashboard redesign batch (p0217–p0222) ([72b1305](https://github.com/holgerleichsenring/agent-smith/commit/72b13055132539b2d2cb2c524d9393fa42a9ba95))
* dense dashboard type scale, replace ad-hoc text-[Npx] (p0217) ([721c22d](https://github.com/holgerleichsenring/agent-smith/commit/721c22d38ece97cf40c401df024b107e64f5e99d))
* LLM activity transparency — intent, tool/target, build-test outcome (p0222) ([5a3cdb1](https://github.com/holgerleichsenring/agent-smith/commit/5a3cdb182c7fad6d9bb4679c4046ac630de15edf))
* scoped shared event store, kill system firehose wipe (p0218) ([739d1a3](https://github.com/holgerleichsenring/agent-smith/commit/739d1a3e3cc2f077deeec2b9134992df862d455d))
* shared Button/Card/Badge/Chip kit + pipeline heading (p0219) ([596858f](https://github.com/holgerleichsenring/agent-smith/commit/596858f9ae5234a5c6b1669a80dc979f44baf367))
* uniform content-shell + tokenised labels/breadcrumbs (p0220) ([38467b3](https://github.com/holgerleichsenring/agent-smith/commit/38467b3012f74d1af4262fc128da325a668b16d1))

## [0.80.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.79.0...v0.80.0) (2026-06-04)


### Features

* default bare C#/.NET sandbox to the .NET 9 SDK (p0215) ([#271](https://github.com/holgerleichsenring/agent-smith/issues/271)) ([21cd574](https://github.com/holgerleichsenring/agent-smith/commit/21cd574817589346e18afd021cc4ea6ce5deb220))
* drop rigid projectmap test gate + dashboard redesign phase specs (p0216–p0220) ([#272](https://github.com/holgerleichsenring/agent-smith/issues/272)) ([9734a0b](https://github.com/holgerleichsenring/agent-smith/commit/9734a0bf147802b063786ea60bbd31d2fa1da3a3))

## [0.79.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.78.0...v0.79.0) (2026-06-03)


### Features

* derive command working directory from module paths (p0212) ([#268](https://github.com/holgerleichsenring/agent-smith/issues/268)) ([163af44](https://github.com/holgerleichsenring/agent-smith/commit/163af443f2e379b8e047b3987bef34b19ad86abf))

## [0.78.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.77.0...v0.78.0) (2026-06-03)


### Features

* dashboard batch — runs list, system app-rail, catalog contents, run metadata (p0208–p0211, p0209a-c) ([#266](https://github.com/holgerleichsenring/agent-smith/issues/266)) ([736c21c](https://github.com/holgerleichsenring/agent-smith/commit/736c21c0437e8ccbc98bde6a12726b503a9f787c))
* two-pane run-detail + visible Load-catalog step (p0205) ([#265](https://github.com/holgerleichsenring/agent-smith/issues/265)) ([d3de006](https://github.com/holgerleichsenring/agent-smith/commit/d3de006559585d87de1316d6727e3409968991c8))

## [0.77.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.76.0...v0.77.0) (2026-06-03)


### Features

* remove silent embedded-prompt fallback for migrated master prompts ([#263](https://github.com/holgerleichsenring/agent-smith/issues/263)) ([37aa806](https://github.com/holgerleichsenring/agent-smith/commit/37aa806a01f94b5a48a74c1811f9d09719a5cb87))

## [0.76.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.75.0...v0.76.0) (2026-06-03)


### Features

* api-security-scan docker-tier — passive-mode + source-mode (p0199f) ([d63a030](https://github.com/holgerleichsenring/agent-smith/commit/d63a0301f9934a8195fad36fedde7ef69a9a4074))
* **events:** StepStartedEvent gains DisplayName + central CommandDisplayNames map (p0203) ([2b64818](https://github.com/holgerleichsenring/agent-smith/commit/2b64818c69080d60c4c8fd05834a9f0aae63d681))
* **legal-analysis:** insert InstallDependencies before BootstrapDocument (p0199e) ([3ba0bca](https://github.com/holgerleichsenring/agent-smith/commit/3ba0bcab50b2076430fdca24e0e5f88e5d4f397e))
* **p0199d:** checked-in skill-catalog fixture unblocks init-project + autonomous fast tier ([fc88894](https://github.com/holgerleichsenring/agent-smith/commit/fc888941b348a4e4c07d659c0802460a80f31c89))
* **p0199d:** docker-tier init-project + autonomous + fixture parser test ([d966695](https://github.com/holgerleichsenring/agent-smith/commit/d966695ebc555f4eddeeecbfc076355105f9696c))
* **ui:** LLM pairing + per-repo aggregation in useRunExecutionTree (p0203) ([97ba28f](https://github.com/holgerleichsenring/agent-smith/commit/97ba28f7a1685f5a3a70a6b84a94078d5507d1ae))
* **ui:** mirror StepStartedEvent.DisplayName on TS + ActivityRow renders it (p0203) ([3f44068](https://github.com/holgerleichsenring/agent-smith/commit/3f4406852b6ed1962100d99c5bd88cfc2211e86c))
* **ui:** per-repo sandbox blocks collapsed-by-default + clearer placeholder (p0203) ([38005c5](https://github.com/holgerleichsenring/agent-smith/commit/38005c581b8fcc9494641d54e30c68cf69828fdb))
* **ui:** step row renders Message under label + DisplayName label (p0203) ([1e91bb4](https://github.com/holgerleichsenring/agent-smith/commit/1e91bb4d3e1d7d4e7d50549475a4e6a2777604a7))


### Bug Fixes

* **install-deps:** surface failure detail to logs + aggregate result ([3926114](https://github.com/holgerleichsenring/agent-smith/commit/39261145a6297c20354e6c3bd7f7e44d2ad6cbdf))
* **skill-manager:** drop misplaced LoadContext step; preset still needs deeper rework (p0204 partial → p0204a) ([0b0e701](https://github.com/holgerleichsenring/agent-smith/commit/0b0e701b4175603aba50b14f76d3f72a1c78415e))

## [0.75.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.74.2...v0.75.0) (2026-06-02)


### Features

* durable ci.install_command read at discovery time (p0202a) ([0bd3b4f](https://github.com/holgerleichsenring/agent-smith/commit/0bd3b4fea604b6608c9f15aab998e14e1d2ce68a))
* install-dependencies step + test/persist aggregation fixes (p0202) ([f92b3ae](https://github.com/holgerleichsenring/agent-smith/commit/f92b3aee4dd81ebd0f650152e3234cd578697bd7))
* PersistWorkBranch recovery recognizes AgenticMaster (p0202c) ([ad99065](https://github.com/holgerleichsenring/agent-smith/commit/ad9906596b9f1a15633b8f71abc18bb28cd11ff9))
* re-init merges context.yaml instead of clobbering (p0202d) ([af13547](https://github.com/holgerleichsenring/agent-smith/commit/af1354794826a7424e95442d22b3e08aa026ec52))


### Bug Fixes

* **analyzer:** test_command references the discovered test project path ([1ce9c2c](https://github.com/holgerleichsenring/agent-smith/commit/1ce9c2cdf2c03b8f1c93f822779c8775a107b748))

## [0.74.2](https://github.com/holgerleichsenring/agent-smith/compare/v0.74.1...v0.74.2) (2026-06-02)


### Bug Fixes

* **dashboard:** wire SandboxVanished into ActivityRow + FilterRail + eventFilterQuery ([47be1fe](https://github.com/holgerleichsenring/agent-smith/commit/47be1fefa36ed0598eb0a1f22cc18ec2ce26d7a4))

## [0.74.1](https://github.com/holgerleichsenring/agent-smith/compare/v0.74.0...v0.74.1) (2026-06-02)


### Bug Fixes

* **ci:** AgentSmithConfigCompositionTests swaps Redis-backed system event publisher too ([96cba6d](https://github.com/holgerleichsenring/agent-smith/commit/96cba6d8d1718c4ea380ada975152449cb244708))
* **ci:** harness fast tier doesn't require Redis (RedisExtensions lazy + harness swaps Redis-backed services) ([a28f8ee](https://github.com/holgerleichsenring/agent-smith/commit/a28f8ee336c5bf60c4fa1dabfe72cfe976185c24))

## [0.74.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.73.0...v0.74.0) (2026-06-02)


### Features

* **fix-bug:** insert PersistWorkBranch between AgenticMaster and Test (operator-reported gap) ([abd4d80](https://github.com/holgerleichsenring/agent-smith/commit/abd4d803a381b7f607d12c08b8d88c42c6caaac8))
* **harness-docker:** add-feature end-to-end (p0199c) ([f8071b4](https://github.com/holgerleichsenring/agent-smith/commit/f8071b428b9a8bebfdc076b1dfd2b295d5745d9b))
* **harness-docker:** fix-no-test end-to-end (p0199c) ([890a76d](https://github.com/holgerleichsenring/agent-smith/commit/890a76d50f35f9203a73296b78104948dc91d4d4))
* **harness-docker:** four deferred presets land as loud-skip facts (p0199c) ([b146457](https://github.com/holgerleichsenring/agent-smith/commit/b146457d1b86c01a1741c035e32015737cad52f5))
* **harness-docker:** real DockerSandbox tier for fix-bug end-to-end (p0199b) ([73e6417](https://github.com/holgerleichsenring/agent-smith/commit/73e64170d9e68ac039cef01bf7c5ea540eae0053))
* **harness-docker:** security-scan + mad-discussion end-to-end (p0199c) ([2731c4e](https://github.com/holgerleichsenring/agent-smith/commit/2731c4e95bd394646dd24410cb79f2914e0cf8a7))
* **harness-docker:** startup env validation + banner for --docker mode ([cb90a8f](https://github.com/holgerleichsenring/agent-smith/commit/cb90a8faafce40e0dfb5ac86b87b067e21ebfa14))
* **harness:** cover fix-bug end-to-end (p0199a) ([0a2f879](https://github.com/holgerleichsenring/agent-smith/commit/0a2f87993459104fa790117ea4985b1bd051db63))
* **harness:** cover fix-no-test, add-feature, mad-discussion (p0199a) ([4f8a39f](https://github.com/holgerleichsenring/agent-smith/commit/4f8a39facbd8e4b527f29e9616e4a8ce12155f3c))
* **harness:** cover security-scan, api-security, legal-analysis (p0199a) ([1b909e5](https://github.com/holgerleichsenring/agent-smith/commit/1b909e53d1cd702fc6ca86907504431665e2f5af))
* **harness:** docker-tier fix-bug coverage + loud-skip plumbing (p0199a) ([9a6db4c](https://github.com/holgerleichsenring/agent-smith/commit/9a6db4c00a26514906b09d941ea405bc674aeb8b))
* **harness:** real-composition pipeline harness foundation + close p0180/p0198 (p0199 milestone-1) ([8181392](https://github.com/holgerleichsenring/agent-smith/commit/8181392ab318331bfd6dd128b66352e102630e3c))
* **harness:** standalone console runner executes presets (p0199a) ([aa3050d](https://github.com/holgerleichsenring/agent-smith/commit/aa3050d6faff137fb4c44a19d4dbfcfa1124d769))
* **liveness:** sandbox heartbeat + orphan reaper + cancel-reason (p0201) ([c3fb6e1](https://github.com/holgerleichsenring/agent-smith/commit/c3fb6e1e361e0e01b378639f6e01304c7550eabe))
* pipeline cancel + watchdog + recent-list hygiene (p0200) ([b69c767](https://github.com/holgerleichsenring/agent-smith/commit/b69c7670d0c4a0dc896d6ce1a03520135cf5768b))
* **sandbox-spec:** ExtraBinds field for test-only bind mounts (p0199b) ([c4c22e0](https://github.com/holgerleichsenring/agent-smith/commit/c4c22e010ae4166b6a2b7a7ee637db710a4d21ed))


### Bug Fixes

* **cancel:** idempotent stale-snapshot clearing on cancel (p0200-followup) ([8af52a2](https://github.com/holgerleichsenring/agent-smith/commit/8af52a2e330b2471a6ad5d88276d4183afa49f63))
* **sandbox-config:** raise StepTimeoutSeconds default 120s→900s ([332a84d](https://github.com/holgerleichsenring/agent-smith/commit/332a84de90a0c94a2a50e5552bb8328daadbb716))
* **server:** AgentSmithConfig override must come AFTER AddCoreDispatcherServices (p0198-followup-2 + p0199 step 1) ([34171b3](https://github.com/holgerleichsenring/agent-smith/commit/34171b3c56456f2c3c43362e2ef41d0948ef2fc0))
* **server:** override AgentSmithConfig.Empty() placeholder with loaded YAML (p0198-followup) ([45c38e4](https://github.com/holgerleichsenring/agent-smith/commit/45c38e4623208c26ae04a4f51de68411b07cdb54))
* **test-handler:** bump TestTimeoutSeconds 300→900 (band-aid until p0200) ([92a2aaa](https://github.com/holgerleichsenring/agent-smith/commit/92a2aaa49306b5ccdb9c20a53f71c2664aeb1146))

## [0.73.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.72.0...v0.73.0) (2026-06-02)


### Features

* **registry-auth:** deterministic pre-stage credentials before build/test (p0198) ([d384f07](https://github.com/holgerleichsenring/agent-smith/commit/d384f07c9225011d89465de05d9d8b44c0fd5c0d))


### Bug Fixes

* **sandbox:** drop -slim variants for node/python so git is in the image (p0193-followup) ([0e0f79c](https://github.com/holgerleichsenring/agent-smith/commit/0e0f79c132d8a3e7c0e8ad7aee713e04c0cb4b57))

## [0.72.0](https://github.com/holgerleichsenring/agent-smith/compare/v0.71.2...v0.72.0) (2026-06-01)


### Features

* **context-yaml:** typed write path eliminates LLM-generated parse failures (p0193) ([90d4a86](https://github.com/holgerleichsenring/agent-smith/commit/90d4a8610b05a9540a5f6a3fa6a8182b246eaae0))

## [0.71.2](https://github.com/holgerleichsenring/agent-smith/compare/v0.71.1...v0.71.2) (2026-06-01)


### Bug Fixes

* **dashboard:** invert /system freshness bar so active subsystems read as full (p0190) ([#253](https://github.com/holgerleichsenring/agent-smith/issues/253)) ([b5656f7](https://github.com/holgerleichsenring/agent-smith/commit/b5656f7ea9b5d8a36e5eaf904a10139e27aa5a7b))

## [0.71.1](https://github.com/holgerleichsenring/agent-smith/compare/v0.71.0...v0.71.1) (2026-05-31)


### Bug Fixes

* **deps:** downgrade M.E.AI to 10.3.0 for Anthropic.SDK compat + tail fallbacks + agent visibility (p0185+p0186) ([#250](https://github.com/holgerleichsenring/agent-smith/issues/250)) ([90da19b](https://github.com/holgerleichsenring/agent-smith/commit/90da19b2d6ebc97c302d2f916fd6a9b593326240))

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
