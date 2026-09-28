# 2026-09-28-89f1 — defect analysis (read before implementing any 89f1 slice)

Found while mirroring the docs (2026-09-28-057da/b), then analysed read-only against HEAD c35dfa5a.
Each entry: facts (file:line), root cause, the fix chosen, tests, docs. The operator's open
questions were settled by the orchestrator; the rulings are in the slice specs' decisions.
The code is the truth — re-check every line number before editing.

## Slice a — PR-comment author trust (C1)
- GitHub checks author_association against a fixed set OWNER, MEMBER, COLLABORATOR, CONTRIBUTOR
  (GitHubPrCommentWebhookHandler.cs:28-34, checked :57-67). CONTRIBUTOR = "had a commit merged",
  no write access → drop it.
- GitLabMrCommentWebhookHandler.cs:41 reads user.username, AzureDevOpsPrCommentWebhookHandler.cs:38
  reads comment.author.uniqueName — neither checks. Both call CommentIntentParser.ParseAsync for any
  comment (GitLab :48, AzDO :50) — an LLM call (CommentIntentParser.cs:53) — and /approve /reject from
  anyone are published as dialogue answers (WebhookDialogueRouter.cs:42-49; AnsweredBy never checked).
- WebhookSignatureVerifier.cs:81-84 verifies the sender (X-Gitlab-Token, AzDO Basic), fail-open with
  no secret (:33). Signature ≠ author.
- Root cause: p0059 specified the check for GitHub only; p0059b/c never carried it.
- Fix: port IPrCommentAuthorTrust (Contracts) CheckAsync(platform, repoRef, authorRef, ct) → trusted?,
  per-platform impls in Infrastructure, called BEFORE the parser, fail closed (exception, missing
  token, unmatched repo = untrusted). Repo→project/credentials match like
  PrReviewRouteResolver.FindMatchingRepo. GitLab: GET /projects/:id/members/all/:user_id,
  access_level >= 30 (payload has user.id, project.id). AzDO: effective Git Repositories
  "Contribute" (bit 4) for resource.comment.author.id via SecurityHttpClient, namespace
  2e9eb7ed-3c0a-47d4-87c1-0ffdd275fd87, token repoV2/{projectId}/{repoId}. GitHub: the existing set
  minus CONTRIBUTOR, moved into GitHubAuthorAssociationTrust. Cache verdict ~5 min per (repo, author).
- Tests: GitLabMrComment_FromReporter_IsNotHandled_AndParserNeverCalled, _FromDeveloper_StartsPipeline,
  _MemberLookupFails_IsNotHandled, _RepoNotConfigured_IsNotHandled, AzureDevOpsPrComment_WithoutContribute_
  IsNotHandled_AndParserNeverCalled, _WithContribute_StartsPipeline, _AclLookupFails_IsNotHandled,
  GitLabMrComment_ApproveFromNonMember_PublishesNoAnswer, AzureDevOpsPrComment_ApproveFromNonContributor_
  PublishesNoAnswer, GitHubPrComment_ContributorAssociation_IsNotHandled.
- Docs: reference/integrations/pr-comments.md:53 (replace disclaimer with per-platform rule),
  reference/configuration/webhooks.md §PR comments, trigger-it/webhooks.md (token scopes: GitLab
  read_api, AzDO PAT Security (Read)).

## Slice b — tracker stamp, routing, dead parent link (C2, C3, C4)
- C2 renamed approved-set stamp ignored: AmendedSpecification.cs:21,29,51 (note names default stamp);
  FiledTicketLabels.cs:69-73 CarriesApprovedSet (FiledTicketSpecGate.cs:60 — renamed-stamp ticket with no
  record derives its own spec instead of parking: the harmful one); FiledTicketLabels.cs:88-90
  BindsPhaseExecution (ProjectResolver.cs:69); MissingSpecReason.cs:37; TicketLabelNote.cs:76 default.
  Correct already: ApprovedSetTicketFiler.cs:69, DiscoveryLabelGuard.cs:26-27, RoutingWordCollisionRule.
  Root cause: 3c7ac left three readers on the constant; 8e51e built on a branch without 3c7ac.
  Fix: thread TicketLabelVocabulary.For(project.Tracker); match with vocabulary.IsApprovedSetStamp
  (configured + historical). Not global across trackers (a renamed word on tracker A may be a routing
  word on B — f6c2's per-tracker collision rule).
  Tests: AmendedSpecification_RenamedStamp_NoteNamesTheRenamedLabel, FiledTicketSpecGate_RenamedStampNoRecord_
  Parks, ProjectResolver_RenamedStamp_BindsToCode, ProjectResolver_RenamedStampOnOtherTracker_DoesNotBind,
  MissingSpecReason_RenamedStamp_NamesIt. Docs: trigger-it/labels.md:44,:67.
- C3 init-project IS label-startable (p0133; InitProjectLabelTriggerSmokeTests) but 2026-09-25-3c7ad put it
  in NeedsHostSuppliedContext (PipelinePresets.Routing.cs:24-25), so the studio cannot offer it. Nothing
  refuses spec-dialog in a label map (RoutingPipelineNames.cs:51 only checks existence; ConfigDraftRules
  has no pipeline check). Fix: remove init-project from the set; advisory startup finding +
  ConfigDraftRules field finding for a label/default_pipeline naming spec-dialog. Invert
  RoutingPipelineChoiceTests.cs:22,31. Tests: Routable_ContainsInitProject, RoutingPipelineNames_SpecDialogIn
  LabelMap_ReportsTheEntry, ConfigDraftRules_TrackerMapsLabelToSpecDialog_FindingNamesLabelAndValue,
  ConfigDraftRules_TrackerMapsLabelToInitProject_NoFinding.
- C4 LinkToParentAsync has no non-test caller since 2026-09-22-b3d7 (decisions/2026-09-22-b3d7.yaml:100-107
  deferred its removal; no planned phase needs it). Delete: ITicketProvider.cs:64 + 4 impls
  (GitHubTicketProvider.cs:116, GitLab…:141, AzureDevOps…:148, Jira…:37,57,154), TrackerParentLink.cs,
  GitHubSubIssueRequest.cs, Domain ParentLinkResult/ParentLinkOutcome, CreatedTicket.NativeId (only the
  sub-issue link reads it), JiraTicketConnection.cs:20,27, TrackerConnection.cs:67-70, RawTrackerEntry.cs:60-62
  (keep as detector), TrackerCatalogBuilder.cs:47, TrackerConnections.cs:28, TrackerEntity.cs:37 (positional
  record — keep param if built positionally), ConfigCatalogMapper.cs:110, RawConfigPatch.cs:79,
  TrackerCapabilityFields.cs:80, ConfigStudioCapabilities.cs:164, schema :220-223, dashboard configApi.ts:473,
  ~17 test doubles, TicketProviderParentLinkTests.cs, TrackerCatalogBuilderTests.cs:72. Loader ignores
  unknown keys (YamlConfigurationLoader.cs:84) → advisory finding for a file still carrying
  parent_link_type (small RetiredTrackerKeys table read off the raw tree). Tests: RetiredConfigKeys_ParentLink
  Type_ReportsAdvisory, TrackerCapabilityFields_NoParentLinkType.

## Slice c — pipelines deliver what they claim (B1–B7)
- B1 auto-fix: SpawnFixContextBuilder.cs:12 passes new AutoFixConfig() since p0060; SpawnFixHandler.cs:31-116
  writes YAML into the sandbox and sets ContextKeys.SecurityFixRequests which nothing reads; no fix run is
  ever started. SecuritySnapshotBuilder.cs:44 FindingsAutoFixed always 0. Fix: DELETE SpawnFixHandler,
  SecurityFixRequestBuilder, SpawnFixContextBuilder, SpawnFixContext, AutoFixConfig, SecurityFixRequest,
  ContextKeys.SecurityFixRequests, CommandNames SpawnFix + every map (CommandStepClasses, CommandModelUse,
  CommandProgressLabels, CommandBeats.OutcomeMap, CommandDisplayNames, ContextBuildersExtensions.cs:80,
  ScanRegistrations, SandboxRequiringCommands), preset entry; FindingsAutoFixed out of snapshot (parser ignores
  unknown keys). Fix stale "SpawnZap (skips if dast not enabled)" comment. Scan→ticket belongs to planned
  p0429b. Tests: SecurityScanPreset_HasNoSpawnFixStep, SnapshotYamlParser_LegacyFindingsAutoFixedKey_IsIgnored.
  Docs: configuration/security-scan.md Auto-fix, pipelines/security-scan.md row + §Auto-fix, configuration/index.md:31.
- B2 confidence_threshold: parsed ProjectPipelineResolver.cs:39-44, carried PipelineDefinition.cs:21,
  PipelineConfigResolver.cs:21-22, ResolvedPipelineConfig.cs:16-18, no reader since 5c68d9aa (p0312a/b/c).
  SkillObservation.Blocking's only reader is the dead VerifyNotesFormatter.Format. Fix: keep
  RawPipelineEntry.ConfidenceThreshold as detector only → Advisory finding "no longer read, remove the key";
  remove from PipelineDefinition/ResolvedPipelineConfig/PipelineConfigResolver; delete
  VerifyNotesFormatter.Format; schema :399-404 + example :455-462. Tests: ProjectPipelineResolver_ConfidenceThreshold
  Set_EmitsAdvisoryRetiredKeyFinding, _Absent_NoFinding; delete two PipelineConfigResolverTests; adjust
  DbConfigStoreTests.cs:181-193. Docs: pr-review.md:12, security-scan.md:126 "stops blocking" wording.
- B3 legal delivery: LegalCommand.cs:18,40 advertises `file` (not registered, OutputStrategiesExtensions.cs:16-19
  → "Unknown output format"); DeliverOutputHandler.cs:34-40 hard-codes "./agentsmith-output" and
  ReportMarkdown null; console/markdown fall back to ConsolidatedPlan which only CompileFindingsHandler.cs:32
  sets (removed from legal in p0179d) → "No findings". The master's analysis.md is in ContextKeys.CodeChanges
  (AgenticMasterHandler.cs:516); only the dead outbox branch reads it. InboxPollingService never registered,
  IInboxJobEnqueuer has no impl (dead since p42). AcquireSourceHandler.cs:32 reads PDF/DOCX with
  File.ReadAllTextAsync (binary corrupted since p0117b). Slack modal (SlackModalSubmissionHandler.cs:72-74)
  starts legal without a document → throws. Fix: DeliverOutput builds ReportMarkdown from the master's
  non-run-record CodeChanges; OutputDirectoryResolver extracted from DeliverFindingsHandler.ResolveOutputDir
  used by both; legal gets --output-dir, only registered formats; delete DeliverToFileAsync,
  WriteToOutboxAsync, ArchiveSource, InboxPollingService(+Options, tests), IInboxJobEnqueuer; AcquireSource
  copies bytes (ISandboxFileReader.WriteBytesAsync, base64); remove the Slack legal modal. mad-discussion is
  fine (delivers discussion.md via CommitAndPR). Tests: DeliverOutput_LegalRun_ConsoleRendersMasterAnalysis,
  DeliverOutput_Markdown_WritesAnalysisToRequestedOutputDir, DeliverOutput_NoMasterDocument_FailsNamingTheMissing
  Report, LegalCommand_UnknownOutputFormat_Rejected, AcquireSource_BinaryPdf_ArrivesByteIdenticalInSandbox.
  Docs: pipelines/legal-analysis.md.
- B4 PR scope: SecurityScanCommand.cs:16,56-57 ScanPrIdentifier never read (d1facdf4); `--branch` help
  "(diff against main)" false. GitHubPrLabelWebhookHandler.cs:40-46 returns security-scan with no
  InitialContext and no project → scans default branch; GitLabMrLabelWebhookHandler.cs:58-61 passes MR iid as
  TicketId; chat `security-review PR#42` PrIdentifier parsed (ChatIntentParser.cs:38,138) and dropped
  (SlackMessageDispatcher.cs:73-79). Working pattern: GitHubPrEventWebhookHandler.BuildInitialContext (72-81).
  Fix: extract PrRunContextFactory, use in both label handlers (+ ProjectName, no iid-as-ticket), carry chat
  PrIdentifier; remove --pr + ScanPrIdentifier; fix --branch help. Tests: GitHubPrLabel_Labeled_SeedsPrNumber
  HeadBranchAndRepo, _NamesOwningProject, GitLabMrLabel_Labeled_SeedsMrContextNotTicketId, SecurityReviewChat
  Intent_WithPr_SeedsPrContext, SecurityScanCommand_HasNoPrOption. Docs: security-scan.md:15, webhooks.md:101,
  pr-comments.md:38, pr-review.md:55, chat-gateway.md:75. NOTE planned p0429b names ScanPrIdentifier — record
  in decisions that PrNumber is the carrier.
- B5 tool_profile: parsed SpawnAgentToolHost.cs:137-139 → SubAgentSpec.ToolProfile, never read; p0280 replaced
  it with SubAgentContext.ChildTools (MasterToolComposition.cs:89). Delete Loop/ToolProfile.cs, the parameter,
  the parsing, the description text. Test: SpawnAgentsTool_Description_DoesNotAdvertiseToolProfile.
- B6 scan exit code: WriteRunResultHandler.cs:217-221,510-521 computes RunDeliveryGate and always Ok;
  AccountScanCoverageHandler.cs:28-42 always Ok; scan presets have no CommitAndPR (the step that fails coding
  runs, CommitAndPRHandler.cs:178-193) → exit 0 on degraded triage / failed audit / outstanding criterion.
  Fix: AccountScanCoverage evaluates RunDeliveryGate.Evaluate(RunAccountLedger.Current, AcceptanceCriteria…)
  and returns Fail(reason); finalizer tail (PipelineFinalizerTail.cs:22-27) still writes result; SARIF already
  written. Findings alone never change exit code. Strict, no opt-out. Tests: AccountScanCoverage_Outstanding
  Criterion_FailsWithGateReason, _AllAnswered_Ok, _NoContract_Ok, SecurityScanCommand_UndeliveredScan_ExitCodeOne.
  Docs: cicd/index.md §Exit codes, security-scan.md:47, api-scan.md account section.
- B7 source: an explicit --source-path that doesn't exist silently goes passive (TryCheckoutSourceHandler.cs:51);
  --source-url without --source-type ignored in agent mode (ExecutePipelineUseCase.cs:461, SourceConfigOverrider.
  cs:56); placeholder /var/empty/agentsmith-noop leaks into the log. Fix: missing explicit path fails; reject
  --source-url without --source-type at parse; plain "no source given — passive mode" message. Tests:
  TryCheckoutSource_CliSourcePathMissing_Fails, _NoSourceGiven_PassiveWithPlainMessage, SourceOptions_SourceUrl
  WithoutType_Rejected. Docs: api-scan.md §Source resolution, trigger-it/cli.md.

## Slice d — config schema, models, providers (D1–D5)
- D1 OpenAiChatClientBuilder.cs:57-60 ignores agent.Endpoint (regression p0119a 8b20d6c6, undocumented).
  ResolveApiKey (84-95) reads api_key_secret as an ENV VAR name, empty → silent fallback to OPENAI_API_KEY.
  Fix: type openai + endpoint → OpenAIClientOptions.Endpoint; with endpoint set never fall back to
  OPENAI_API_KEY (named-but-empty var throws naming it; no api_key_secret → placeholder key). Keep default
  rate budget (LlmRateBudget.cs:31-32), document rate_limit/context_window_tokens/pricing. Delete unread
  ModelAssignment.Endpoint (ModelAssignment.cs:13); keep ProviderType (read ChatClientFactory.cs:72,104).
  Tests (ChatAdapterWireTests testTransport): OpenAi_WithEndpoint_SendsToThatHost, _WithoutEndpoint_SendsTo
  ApiOpenAiCom, _EndpointWithEmptyNamedSecret_ThrowsNamingTheVariable, _EndpointWithoutSecret_NeverSendsOpenAi
  ApiKey. Docs: ai-providers.md "OpenAI-compatible" section (type openai + endpoint), agentsmith-yml.md:68-69.
- D2 schema drift: loader IgnoreUnmatchedProperties (RawConfigYaml.cs, YamlConfigurationLoader.cs:84) so the
  schema is the only typo guard and nothing tests it; the example fails its own schema (persistence, dialogue,
  tool_runner, pipeline_cost_cap undeclared at a root with additionalProperties:false); export emits trace/
  role_mapping the schema rejects; agent.parallelism still in schema. Full add-list: root dialogue,
  persistence, pipeline_cost_cap, mcp_servers, registries, role_mapping, trace, tool_runner; agent
  max_fix_iterations, supports_vision, max_master_loop_iterations, max_sub_agent_loop_iterations,
  ledger_reminder_every_n_iterations, reminder_drift_editless_iterations, verdict_owed_after_iterations,
  worker_structured_result, rate_limit{requests_per_minute,input_tokens_per_minute}, scan_* keys; type enum +
  external_worker (+ aliases anthropic, google); agent sub-blocks retry/cache/compaction/models/pricing
  (bare today); tracker needs_clarification_status, not_implementable_status, default_pipeline,
  lifecycle_status_names, label_names; project templates[...], polling, orchestrator (bare);
  project.sandbox agent_registry, agent_version, step_timeout_seconds, run_command_timeout_seconds;
  webhookTrigger failed_status, needs_clarification_status, not_implementable_status, in_progress_status;
  pipelineEntry coding_principles_path; sandbox global agent_registry, agent_version, step_timeout_seconds,
  run_command_timeout_seconds, max_concurrent_sandboxes, allowed_registries, allow_docker_hub_library,
  image_pull_secrets, hold_seconds; skills source/version/path/url/sha256/overlay/cache_dir; limits (13 keys of
  LoopLimitsConfig); queue/pipeline_storage/pipeline_data_flow/orchestrator; dialogue hot_wait_seconds,
  approval_timeout_seconds, dashboard_url. Remove: agent.parallelism. tracker.endpoints: FIX THE CODE —
  RawTrackerEntry has no Endpoints, TrackerCatalogBuilder never maps it (since 04ebc70a) → add + map +
  studio. Test: new Architecture/ConfigSchemaFile.cs (mirror ContextSchemaFile.cs, JsonSchema.Net) +
  Configuration/ConfigSchemaCoverageTests: ConfigSchema_DeclaresEveryKeyTheLoaderBinds (reflect from
  RawAgentSmithConfig with UnderscoredNamingConvention; dict→additionalProperties, list→items, enums; ToolRunner
  Config as 2nd root), ConfigSchema_DeclaresNothingTheLoaderIgnores (allowlist w/ reasons),
  ExampleConfig_ValidatesAgainstSchema, ExportedConfig_ValidatesAgainstSchema; then additionalProperties:false
  on every def. Fix RetiredParallelismConfigTests claim.
- D3 example.yml stale: l.6-11 bootstrap reads also auth:, tool_runner from file; l.18 dead docs link; l.21
  providers missing copilot, external_worker; l.59,77 .agentsmith/context.yaml → contexts/<name>/; l.165-167
  "no screen grants a role" false (Access → Permissions → People); l.179-181 "Settings -> Roles & claims" →
  Access / Permissions (/config/access); l.191-192 outside-catalog permission: refused on the Access page,
  dropped + finding via auth: seed/import; l.236-238 Haiku 4.5 price $1/$5; l.215 compaction.summary_model
  dead; l.253-265, 288-308 models blocks walk into the D5 trap; l.267 claude-parallel orphaned (parallelism
  removed p0312d); l.384-389 "loader refuses to start" → blocking finding, trigger disabled (ClarificationPark
  StatusRule.cs:9-12); l.421-425 Jira endpoints (fixed by D2); l.507-509 p0140 note stale; l.557-563 ACTIVE
  `skills: {source: default, version: v4.7.0}` downgrades from embedded v5.7.3 → comment out, fix "pinned
  v4.0.0"; l.560 dead docs link; l.577-578 spawn_agents "opt-in per pipeline" false (every master while
  max_sub_agents_per_run>0, MasterToolComposition.cs:78-79); l.588-591 per_tier defaults + doc path. Tests:
  ExampleConfig_ValidatesAgainstSchema, ExampleConfig_NamesNoDocsPathThatDoesNotExist.
- D4 trace: ConfigDocumentTaxonomy.All has no Trace → DB config yields trace.enabled=false; import drops trace
  silently, export emits it. Fix: ConfigDocDescriptor.Singleton(ConfigDocTypes.Trace…), settings.ts
  SETTING_KEYS/labels + configApi SettingKey; AGENTSMITH_TRACE keeps winning; import reports keys it drops
  (tool_runner too — it stays bootstrap/file-read, documented). Tests: DbConfigurationLoader_StoredTraceEnabled_
  YieldsTraceOn, ConfigImport_TraceBlock_RoundTripsThroughStore, TraceSwitch_EnvOff_WinsOverStoredOn,
  Taxonomy_CoversEveryRawRootProperty_OrNamesWhyNot (allowlist auth, tool_runner with reasons).
- D5: NewFormatSkillValidator.cs:56 message omits master (build from ordered AllowedRoles; test
  ValidateRole_Unknown_MessageListsEveryAllowedRole). YamlSkillLoader.cs:216 dead docs path → docs/reference/
  configuration/skills.md; add SourceNamesNoDocsPathThatDoesNotExist over string literals in src/.
  AllHostsActivePolicy.cs comment wrong (children never get read_sub_agent_observations; gating is the fan-out
  count). CompactionConfig.cs:41,49 SummaryModel/DeploymentName dead since p0119a → extend
  CompactionConfigDeprecationWarner, drop from studio entity/patch (ConfigCatalogMapper.cs:73, RawConfigPatch.cs:47)
  or mark deprecated. MODELS TRAP: ModelRegistryConfig.cs:9-33 initialises roles with Claude ids; a partial
  models: block keeps them (also RawAgentModelPatch.cs:19) → OpenAI/Copilot/Gemini agents get claude-haiku for
  scout/summarization; a block without primary ignores agent.model (BuildFallback only);
  ConfiguredAgentCheck.ResolvedModel disagrees with registry. Fix: every role ModelAssignment? with no built-in
  model; primary → agent.model; scout/planning/summarization/reasoning/context_generation → primary;
  code_map_generation → scout → primary; one startup finding naming inherited roles. Files ModelRegistryConfig.cs,
  ConfigBasedModelRegistry.cs, ChatClientFactory.cs:238-250, RawAgentModelPatch.cs, ConfiguredAgentCheck.cs,
  StartupSummaryLogger.cs:69. Tests: PartialModels_OpenAiAgent_NoRoleResolvesToAClaudeId, ModelsWithoutPrimary_
  PrimaryIsAgentModel, CodeMap_Unset_FollowsScoutThenPrimary, Probe_PartialModels_UsesAgentsOwnModel. Docs:
  ai-providers.md:28-34, agentsmith-yml.md:77-87.

## Slice e — server surfaces that lie or are dead (A1, A3, A4, A5)
- A1 /health: only GET /health (WebApplicationExtensions.cs:13); HealthResponseBuilder unused since p0107;
  probes all hit /health (deploy/k8s/8-deployment-server.yaml:228-241, compose :190, ingress :42) — no 404.
  Subsystem healths computed (Hosting/PollingExtensions.cs:19-45) never exposed. Fix: /health (always 200)
  gains `subsystems` [name, state, reason, last_changed_utc] via new Server/Services/SubsystemHealthSection.cs
  (style of PreflightHealthSection); delete HealthResponseBuilder; fix comments SubsystemState.cs:5,
  ISubsystemHealth.cs:6. No /health/ready (it would pull the only pod that can report the fault).
  Tests: SubsystemHealthSection_EverySubsystem_ListedWithStateAndReason, _NoneRegistered_EmptyArray,
  HealthEndpoint_RedisDegraded_Still200_BodyNamesRedisDegraded. Docs: server-resilience.md, docker-compose.md:91,
  kubernetes.md:37, architecture/layers.md:146.
- A3 retired names: Cli/Program.cs:29 AutonomousCommand → fails at ExecutePipelineUseCase.cs:270. Two lists:
  PipelinePresets.RetiredPresets/RetiredReason (PipelinePresets.cs:121-136, test-only callers) and
  RetiredPipelineNames.cs (e5b1). Fix: one list — move removed presets into RetiredPipelineNames as Removed
  (name→reason), delete RetiredPresets/RetiredReason, add RetiredPipelineNames.Explain(name) and call it from
  ExecutePipelineUseCase:270, Cli/DryRunPrinter.cs:17, RoutingPipelineNames.Instead (:61-64),
  AgentSmithConfigValidator.cs:90-95,127-131 (fix false comment :124-125). Delete AutonomousCommand + Program
  line, AutonomousConfig.cs, AutonomousFinding.cs, tests/AgentSmith.PipelineHarness/Presets/AutonomousDockerTests.cs;
  HarnessPresetNameTests → RetiredPipelineNames. Tests: ExecutePipeline_RemovedPresetName_RefusalCarriesReason,
  _RenamedPresetName_RefusalNamesCode, RoutingPipelineNames_RemovedPreset_FindingCarriesReason,
  ConfigValidator_ProjectPipelineRetired_MessageNamesReplacement, CliRoot_HasNoAutonomousVerb; update
  PresetAliasRemovalTests. Docs: trigger-it/cli.md:31, layers.md:130, pipelines/index.md:32, DESIGN.md
  pipeline-autonomous token (:297,545,548,588).
- A4 dashboard: CapacityFootprintPanel unmounted by p0343c; p0348 deliberately moved pods to RunSideRail.tsx
  Compute drawer (live pods, reservation only while calculating); lost: `dropped` contexts + reason (backend
  still sends, RunFootprintView.cs:11-17). RunsList replaced by MissionControl in p0343; ClearTerminalRunsButton
  lost; DELETE /api/runs?state=terminal has no UI caller. RunsList.tsx survives for mergeNewestFirst (5
  importers); OverviewCardGrid.tsx is an unused re-export shim. Fix: mount "Clear finished" in MissionControl
  Finished section header (only when finished > 0; runs.delete) via an `action` slot on Section — extract
  Section to components/jobs/mission/Section.tsx (MissionControl.tsx is 251 lines); move mergeNewestFirst to
  src/lib/runs/mergeNewestFirst.ts; delete RunsList.tsx, OverviewCardGrid.tsx, RunsList.test.tsx (keep merge
  cases), CapacityFootprintPanel; add dropped contexts + reason to the Compute drawer in both states. vitest:
  MissionControl_FinishedRuns_ShowsClearFinished, _NoFinishedRuns_HidesClearFinished, RunSideRail_ComputeDrawer_
  ListsDroppedContextsWithReason, mergeNewestFirst_LiveWinsOnId_NewestFirst. Docs: operations/dashboard.md.
- A5: KnowledgeBaseConfig dead since p61 → delete + test. QueryKnowledgeHandler born orphaned (p61) → delete
  handler, QueryKnowledgeContext(+Builder), CommandNames.QueryKnowledge and its map entries (CommandBeats,
  CommandStepClasses, CommandDisplayNames, CommandProgressLabels, SandboxRequiringCommands). compile-wiki stays
  (planned 2026-09-08-c56e retires it). ExpectationMetricsEndpoints (GET /api/runs/expectations/metrics →
  OverviewView "Criteria met") reads RunExpectation rows no longer written since p0393 (NegotiateExpectation
  removed from presets; handler, context, builder, ExpectationDrafter, recorder survived). Fix: delete the
  NegotiateExpectation chain + dead ContextKeys.RunExpectation read branches (AcceptanceCriteria.cs:36-39,
  ExpectationPromptSection.cs:42-44, ExpectationPrBodySection, WriteRunResultHandler); KEEP
  ExpectationRatifiedEvent contract, RunEventApplier/RunExpectationProjection, dashboard verifyFallback/
  VerifySummary (archived trails decode); re-source the metric from Run.AcceptanceJson with
  RunCriterionJudgement overrides — same endpoint/shape: share of criteria met, per project per month; keep the
  RunExpectations table (history). Tests: ExpectationMetrics_AggregatesFromAcceptanceSnapshot_PerProjectPerMonth,
  ExpectationMetrics_HumanJudgementOverridesMachineStatus, CodePreset_NoHandlerRegisteredForNegotiateExpectation;
  delete NegotiateExpectationHandlerTests, adjust DurableDialogueHarness/Tests. Docs: dashboard.md:125.

## Slice f — chat runs go through the queue (A6, A2)
- Ticket runs (webhook, poller, dashboard init/resume, capacity queue): TicketClaimService / InitRunLauncher /
  ResumeRunLauncher → Redis queue → PipelineQueueConsumer.cs:84 → ExecutePipelineUseCase in the server, sandbox
  backend via ServerSandboxExtensions.ResolveBackend() (SANDBOX_TYPE, else k8s host, else docker socket).
- Chat runs (FixTicketIntentHandler, InitProjectIntentHandler; Slack/Teams) → IJobSpawner (SPAWNER_TYPE) → a
  Job running the CLI image `run --headless --job-id --redis-url` (KubernetesJobSpawner.cs:154-170); CLI
  composition registers only AddInProcessSandbox (Cli/ServiceProviderFactory.cs:61) → builds inside the CLI
  Job container (only .NET SDK + git), no toolchain images, no secrets, no sizing, no reaping, no capacity
  admission, no config mount (server config is in the DB since p0349), and the image name is wrong:
  AgentImageDefaults.cs:16 "agentsmith-cli" vs published holgerleichsenring/agent-smith-cli
  (docker-publish.yml:11,70), read by OrchestratorImageResolver.cs:36-37 (FixTicketIntentHandler.cs:67,
  InitProjectIntentHandler) → ErrImagePull. Tests pin the wrong name (OrchestratorImageResolverTests.cs:19,37,64).
  RunFootprintCalculator.cs:33-34 adds a phantom orchestrator pod to every ticket run. JobSpawnerSandboxProbe.cs:4-8
  probes the spawner, not the ISandboxFactory. Root cause: p0107 moved queued runs into the server; the chat path
  was never moved. Memory rule: the CLI is a dev tool, not a spawned runtime.
- Fix: FixTicketIntentHandler → ITicketClaimService (claim, admission, enqueue); InitProjectIntentHandler →
  InitRunLauncher; chat progress/questions follow the run id (questions via the durable dialogue that parked
  runs use). Delete IJobSpawner, Kubernetes/Docker/UnavailableJobSpawner, JobSpawnerSetup, JobSpawnerOptions
  (+Extensions), JobRequest, OrphanJobDetector, OrchestratorImageResolver/IOrchestratorImageResolver/
  OrchestratorImageName, OrchestratorResourceResolver, the orchestrator pod in RunFootprintCalculator, the
  CLI's hidden `run --headless --job-id/--redis-url` mode (CliInteractionRegistration, CliRunRecordingRegistration),
  the JobId branch in RunTerminator/RunDeleter (only once no live row carries one — keep reading old rows).
  Replace SpawnerProbe/JobSpawnerSandboxProbe with a probe of the composed sandbox backend. SPAWNER_TYPE,
  AGENTSMITH_IMAGE, JobSpawner__* go; deployment.version's orchestrator half retires (advisory finding if
  set). Tests: FixTicketIntent_EnqueuesThroughClaimService_SpawnsNothing, InitProjectIntent_LaunchesThroughInit
  RunLauncher, ChatConversation_ReceivesProgressForQueuedRunId, RunFootprint_ContainsNoOrchestratorPod,
  PreflightSandboxProbe_ProbesComposedSandboxBackend, ServerComposition_RegistersNoIJobSpawner. Docs:
  kubernetes.md:3,28,40,41,128,133, capacity.md:7,37, chat-gateway.md:26,108, slack.md:127,147,286,
  teams.md:209,233,273, docker-compose.md:36, deploy/DOCKERHUB_DESCRIPTION.md:10,75, manifests
  deploy/k8s/8-deployment-server.yaml:180-190, deploy/docker-compose.example.yml:161.

---

# Adversarial review corrections (binding — they override the sections above where they disagree)

Series re-cut after review: a, b, c, d, e (health/names/leftovers), f (chat launch), g (chat binding),
h (spawner removal), i (Criteria met metric). Order: wave 1 a, b, c (b merges first); wave 2 d (on b+c),
e (on c); wave 3 f, g, h (h last), i (after e).

## a
- The /approve dialogue branch via WebhookDialogueRouter is already dead: RedisConversationLookup.cs:27 looks up
  conversation:{platform}:pr:{repo}#{pr} and nothing writes a pr: channel. Delete that dialogue branch from the
  PR-comment handlers (and its route if nothing else uses it) instead of gating it; drop the two
  *_ApproveFrom…_PublishesNoAnswer tests.
- Order: structural slash-command match (CommentIntentParser.cs:40-55) → trust check → LLM. Ordinary comments
  cost no API call.
- Azure DevOps: SecurityHttpClient.HasPermissions evaluates the CALLER; a user-descriptor ACL query misses
  inherited group grants. SPIKE FIRST: find an API that returns a named identity's EFFECTIVE Git Contribute
  permission (e.g. Security Permission Evaluation / _apis/security/permissionevaluationbatch with a descriptor,
  or the Graph + permissions REST); name the PAT scopes it needs. If no such API exists, record it and fall
  back to: author is a member (direct or via group expansion) of a group that holds Contribute — state it.

## b
- b CREATES the shared retired-key detector (RetiredConfigKeys: key path → since/reason, read off the raw YAML
  tree and off stored documents, emits Advisory findings). c, d and h add rows. b merges first.
- parent_link_type stays on RawTrackerEntry as detector only → d's coverage allowlist.
- Label-started init runs with IsInit=false (ExecutePipelineUseCase.cs:331-335) and the single-result path of
  WriteRunResultHandler.cs:60,87 — accept explicitly in decisions (behaviour unchanged, not a defect reported).

## c
- The chat security-review PR context and the Slack legal modal removal MOVE TO f (f owns every chat file:
  SlackMessageDispatcher, SlackModalSubmissionHandler, FixTicketIntentHandler, JobRequest, ModalIntentFactory,
  chat-gateway.md).
- GitLabMrLabelWebhookHandler returns TriggerInput=null (:49-52; file is 60 lines), matches ANY update on a
  labelled MR and is registered before the MR-event handler (WebhookEndpointsExtensions.cs:29 vs :40); first
  handler wins (WebhookRequestProcessor.cs:118-124) → every push to a labelled MR is swallowed and pr-review
  never runs. Fix: react only when the label is ADDED, return a trigger with PR context. Test
  GitLabMrLabel_SourcePushOnLabelledMr_FallsThroughToPrReview.
- PrRunContextFactory: parse per platform (GitHub vs GitLab payloads differ), share only the context-dictionary
  builder.
- FindingsAutoFixed readers also: SecurityTrendHandler.cs:75-76, SecurityRunSnapshot.cs:14,
  SecuritySnapshotWriter.cs:105, SnapshotYamlParser.cs:85.
- AccountScanCoverage must RunAccountLedger.Record (:32) BEFORE failing.
- No ISandboxFileReader.WriteBytesAsync (≈10 fakes); reuse the base64 pattern of TicketDocumentMaterializer.cs:65-67.
- confidence_threshold: remove the Blocking range check (ProjectPipelineResolver.cs:39-44) or a retired key still
  blocks startup; add ProjectFindings Advisory helper or use b's RetiredConfigKeys row; stored in DB documents
  (RawProjectPatch.cs:91-92) with no studio field → the finding says how to clear it (config export/edit/import).
- file-length-baseline.tsv: remove rows of deleted files; SlackMessageDispatcher.cs is at 120 (not c's any more),
  TryCheckoutSourceHandler.cs at its baseline → extract, don't grow.
- Legal modal removal list (for f): ModalCommandType.cs:13, SlackModalBuilder.cs:97,111,
  SlackModalBlockFactory.cs:14, chat-gateway.md:83, website/src/_data/pipelines.json:361.

## d
- Stored DB configs already hold the Claude ids (YAML import builds new ModelRegistryConfig(); Decompose stores
  it; RawAgentModelPatch.cs:26 ??=). A stored default can't be told from a choice → a startup FINDING per agent
  whose non-Claude type carries Claude ids in stored roles (named roles, one-click clear in the studio), and
  Decompose omits null roles from now on. Done criterion rephrased accordingly.
- Null-dereference sites: ContextWindowThresholdCheck.cs:75-84, ConfigCatalogMapper.cs:39-43,
  ConfiguredAgentCheck.cs:58 (also resolves in opposite order). Enable nullable warnings as errors for touched
  files or add tests that hit them.
- ConfigStudioCapabilities.cs:46-50 marks scout/planning/summarization/code-map required → optional;
  AgentForm.tsx:97 text; ContextGenerationRoleOptionalTests:28.
- ConfigBasedModelRegistry takes agent.model and agent.deployment; code-map ?? Scout ?? Primary; RawAgentModelPatch:
  empty model = unset.
- max_tokens: inheriting roles take primary's max_tokens (8192 default) instead of 4096/2048 → record in decisions;
  consumers SpecDerivationCall, RepoScopeClassifier.
- Endpoint: agent.Endpoint already means Ollama/Azure/external_worker URL and the builder is chosen by
  assignment.ProviderType ?? agent.Type (ChatClientFactory.cs:72,104). KEEP ModelAssignment.Endpoint; effective
  endpoint = assignment.Endpoint ?? (effective type == agent.Type ? agent.Endpoint : null).
- Jira endpoints block also create, issue_link (JiraEndpoints.cs:24,27).
- RoleMappingConfig.IsEmpty get-only → [YamlIgnore]; the reflection test reflects only settable properties.
- Coverage allowlist: detector-only keys parent_link_type, confidence_threshold, orchestrator.* (from h).
- d OWNS config/agentsmith.schema.json and config/agentsmith.example.yml; b, c, h list removals for d, and d
  applies them (if d merges before h, h edits after d).

## e (trimmed)
- DROP "dropped contexts in the Compute drawer": RunFootprintCalculator.cs:67 hard-codes [], scope drops live as a
  PlanDecision (ScopeReposHandler.cs:111-117) and the capacity row is deleted on finish. The fact is in the run's
  decisions already.
- Criteria-met metric MOVES to slice i.
- Clear finished: DeleteTerminalAsync (RunDeletionRepository.cs:22-26) leaves RunCriterionJudgement rows orphaned →
  delete them with their run; the confirm text says finished runs and their verdict history go.
- NegotiateExpectation deletion list also: ExpectationRatifier, ExpectationEditParser, ExpectationQuestionBuilder,
  ExpectationTrackerCommenter + two comment templates and keyed registrations (TicketProvidersExtensions.cs:32-35),
  ExpectationDraftParser/Validator, ExpectationOutcomeRecorder, WriteRunResultHandler:285-286,527. The expectation
  eval harness + goldens go too — name the loss in decisions. Durable-dialogue harness binds AskContextBuilder,
  comments only.
- /health: register capacity_queue (CapacityQueuePumpHostedService.cs:27-29) as a subsystem; delete
  HealthResponseBuilderTests.cs. Done: /health lists queue_consumer, housekeeping, poller, redis, capacity_queue.
- Autonomous leftovers: ContextKeys.Bootstrap.cs:61, DockerPresetRunner.cs:116-121, PresetDeferrals.cs,
  DockerPresetScripts.cs, HarnessTicketSeed.cs, PipelineNameVocabularyTests.cs:48-52, file-length-baseline.tsv:102.
- cli.md = docs/trigger-it/cli.md.

## f / g / h (chat path, re-cut)
- f — chat launch: admission, FIFO and the approved-set carrier live in SpawnPipelineRunsUseCase.cs:61-89
  (IncomingTicketEnvelope + WebhookTriggerConfig), NOT in ClaimAsync. ClaimPreChecker.cs:13-24,47-54 looks up the
  trigger by platform; chat intents carry "slack"/"teams" (ModalIntentFactory.cs:21) → PipelineNotLabelTriggered;
  modal mad-discussion (:74) would be refused. Ticketless chat runs (security-review, modal SecurityReview,
  LegalAnalysis → the legal modal is REMOVED here) cannot be claimed. Needed: a chat launcher: ticket runs map to the
  project's tracker platform and trigger (so statuses resolve, SpawnRequestBuilder.BuildInitialContext) and skip the
  "was it label-routed" check like the dashboard; ticketless runs through a launcher shaped like InitRunLauncher
  (admit, queued row, enqueue, return run id) carrying c's PR context (PrRunContextFactory). Chat fix names no
  pipeline (LlmIntentParser.cs:95) → `code`. Mint the run id in chat and pass ExistingRunId (like
  SpawnPipelineRunsUseCase.cs:67); ClaimResult carries none.
- g — durable run↔chat binding: server runs report through ConsoleProgressReporter (ServerCompositionBuilder.cs:34-36),
  so nothing reaches the bus; BusMessageRouter.cs:92-116 clears the channel only on Done/Error; questions do arrive
  (DialogueJobIdentity → run id). Conversation state expires 45 min (ConversationStateRepository.cs:16), bus
  subscription is per-replica memory, hot question stream 2 h, checkpoint needs a TicketId
  (DialogueCheckpointWriter), answers dropped when state is gone (SlackInteractionHandler.cs:104-110). Needed: a
  DB binding run id → platform/channel/thread, re-subscribed on startup; a terminal notifier from the run's
  terminal event (RunFinalizationProjection or the event stream) posting PR url / failure reason; a channel
  unblocker reading Run status (replacing OrphanJobDetector's IsAliveAsync); questions answered through the
  binding for as long as the run waits.
- h — deletion: spawner family; ConnectionDiagnosticsService.cs:22,65 (dashboard "sandbox" diagnostic), SpawnerProbe,
  ServerPreflightExtensions; tests ServerDiLifetimeTests:188, NonDiCtorRuleTests:209, CancelEnforcementTests,
  RunDeleteTests, SlackModalSubmissionHandlerTests, JobSpawnerOptionsBindingTests, OrchestratorImageResolverTests;
  no sandbox backend has a probe → reuse ISandboxCapacityProbe or add one per backend; deploy/k8s/2-rbac.yaml:19-21
  batch/jobs grant goes. JobSpawnerSetup registers IKubernetes with a kubeconfig fallback that WINS over the
  sandbox's in-cluster-only registration (KubernetesSandboxRegistrations.cs:24, ServerHostFactory.cs:34) → move
  the fallback into the sandbox registrations or out-of-cluster k8s sandboxes break. deployment.version STAYS (it
  feeds sandbox.agent_version, DeploymentDefaultsApplier.cs:31-36, AgentVersionResolver.cs:45); only
  orchestrator.registry/version and projects.*.orchestrator.{registry,version,resources} retire (RetiredConfigKeys
  rows); max_run_wall_time_seconds stays. Orchestrator surfaces: ConfigResolutionPass.cs:46,
  ResolvedProjectSettings.OrchestratorImage, ConfigSnapshot.cs:94, ConfigSnapshotMapper.cs:89, dashboard
  configApi.ts:45, settings.ts orchestrator entries, SettingsForm.tsx:52,114, RawProjectEntry.Orchestrator,
  DeploymentDefaultsApplier.ApplyOrchestrator, schema. CLI normal runs unaffected (CliInteractionRegistration.cs:26-28,
  CliRunRecordingRegistration.cs:36-40); keep RedisEventPublisher; DialogueJobIdentity job-id branch becomes dead.

## i — Criteria met
- ExpectationMetricsSnapshot is ratification counts; a criteria share needs a new response shape and the dashboard
  side rewritten (expectationsApi.ts, expectationTotals.ts, useExpectationMetrics, CriteriaMetCard.tsx,
  ExpectationMetricsView.tsx, ExpectationProjectCard.tsx + tests). Join: CriterionKey.Of(text) hash.
- Definition (ruling): runs of pipeline `code` only; criteria from Run.AcceptanceJson excluding the synthetic
  account row (AcceptanceSnapshot.cs:55-57); share = met / (met + unmet + unproven); not_applicable excluded from
  numerator and denominator; month = run finished date (UTC); an operator overrule counts when its recorded
  MachineStatus equals the snapshot's current status, otherwise it is stale and ignored (shown as stale).
