"use client";

import type {
  ConfigCapabilities,
  CostCapValues,
  DeploymentSetting,
  DialogueSetting,
  LimitsSetting,
  OrchestratorSetting,
  PipelineCostCapSetting,
  PipelineDataFlowSetting,
  TraceSetting,
  PrimaryProviderSetting,
  QueueSetting,
  SandboxSetting,
  SeriesSetting,
  SettingKey,
  PreconditionsSetting,
  BranchCleanupSetting,
  SettingValue,
  SkillsSetting,
  SpecReviewSetting,
  TicketsSetting,
  HoldsSetting,
  PocsSetting,
} from "@/lib/configApi";
import { CheckField, DrawerSection, NumberField, TextField, keep, patcher } from "./formFields";
import { SeriesForm, SpecReviewForm } from "./SeriesSettingsForms";
import { BranchCleanupForm, HoldsForm, PreconditionsForm } from "./SweepSettingsForms";
import { TicketsForm } from "./TicketsSettingsForm";
import { SandboxImageTrustFields } from "./SandboxImageTrustFields";
import { PocsSettingsForm } from "./PocsSettingsForm";
import { useConfigCatalogContext } from "./ConfigCatalogProvider";
import { useT } from "@/hooks/useT";
import type { TextKey } from "@/lib/i18n/translate";

// p0353: the per-settings typed forms. One case per settings key — flat docs are a
// straightforward field list; the nested pipeline-cost-cap (default + per-pipeline +
// per-tier) and the registries LIST render as structured sub-forms. Every field is
// controlled: the parent owns the draft, the form patches it. A required number that
// is momentarily cleared retains its loaded value (via `keep`) instead of drafting a
// silent 0 — the backend validates the shape.

const TIER_ORDER = ["Trivial", "Small", "Medium", "Large", "Xl"] as const; // 2026-11-10-548f: Xl

const SKILLS_SOURCES: { value: number; label: TextKey }[] = [
  { value: 0, label: "config.skills.source.default" },
  { value: 1, label: "config.skills.source.path" },
  { value: 2, label: "config.skills.source.url" },
  { value: 3, label: "config.skills.source.embedded" },
];

export function SettingsForm({
  settingKey,
  value,
  onChange,
  capabilities,
  persisted = null,
  saveError = null,
}: {
  settingKey: SettingKey;
  value: SettingValue;
  onChange: (v: SettingValue) => void;
  capabilities: ConfigCapabilities | null;
  /** The saved value — the Sandbox form refetches the boundary in force when it changes. */
  persisted?: SettingValue | null;
  /** 2026-11-24-f965: the last refused save's message — the PoCs form places its sentences at their fields. */
  saveError?: string | null;
}) {
  switch (settingKey) {
    case "orchestrator":
      return <OrchestratorForm value={value as OrchestratorSetting} onChange={onChange} />;
    case "sandbox":
      return (
        <SandboxForm value={value as SandboxSetting} onChange={onChange}
          persisted={persisted as SandboxSetting | null} />
      );
    case "deployment":
      return <DeploymentForm value={value as DeploymentSetting} onChange={onChange} />;
    case "primary_provider":
      return (
        <PrimaryProviderForm
          value={value as PrimaryProviderSetting}
          onChange={onChange}
          capabilities={capabilities}
        />
      );
    case "limits":
      return <LimitsForm value={value as LimitsSetting} onChange={onChange} />;
    case "pipeline_cost_cap":
      return (
        <PipelineCostCapForm
          value={value as PipelineCostCapSetting}
          onChange={onChange}
          capabilities={capabilities}
        />
      );
    case "queue":
      return <QueueForm value={value as QueueSetting} onChange={onChange} />;
    case "dialogue":
      return <DialogueForm value={value as DialogueSetting} onChange={onChange} />;
    case "skills":
      return <SkillsForm value={value as SkillsSetting} onChange={onChange} />;
    case "pipeline_data_flow":
      return <PipelineDataFlowForm value={value as PipelineDataFlowSetting} onChange={onChange} />;
    case "trace":
      return <TraceForm value={value as TraceSetting} onChange={onChange} />;
    case "spec_review":
      return <SpecReviewForm value={value as SpecReviewSetting} onChange={onChange} />;
    case "series":
      return <SeriesForm value={value as SeriesSetting} onChange={onChange} />;
    case "preconditions":
      return <PreconditionsForm value={value as PreconditionsSetting} onChange={onChange} />;
    case "branch_cleanup":
      return <BranchCleanupForm value={value as BranchCleanupSetting} onChange={onChange} />;
    case "tickets":
      return <TicketsForm value={value as TicketsSetting} onChange={onChange} />;
    case "holds":
      return <HoldsForm value={value as HoldsSetting} onChange={onChange} />;
    case "pocs":
      return <PocsSettings value={value as PocsSetting} onChange={onChange} saveError={saveError} />;
  }
}

// 2026-11-23-933da: the PoCs form picks its agent, tracker and source repository from the shared catalog.
function PocsSettings({ value, onChange, saveError }: {
  value: PocsSetting; onChange: (v: SettingValue) => void; saveError: string | null;
}) {
  const { catalog } = useConfigCatalogContext();
  return <PocsSettingsForm value={value} onChange={onChange} catalog={catalog} saveError={saveError} />;
}

function OrchestratorForm({
  value,
  onChange,
}: {
  value: OrchestratorSetting;
  onChange: (v: SettingValue) => void;
}) {
  const set = patcher(value, onChange);
  const t = useT();
  return (
    <>
      <NumberField label={t("config.orchestrator.wallTime.label")} value={value.maxRunWallTimeSeconds}
        onChange={(v) => set({ maxRunWallTimeSeconds: keep(v, value.maxRunWallTimeSeconds) })} testId="setting-orchestrator-walltime"
        help={t("config.orchestrator.wallTime.help")} />
    </>
  );
}

// 2026-09-18-0f27: every sandbox setting but the concurrent-sandbox bound is read from
// an options instance built once at startup, so the form says per field which of the two
// it is rather than leaving the operator to learn one rule that is no longer uniform.
// Their words are part of each field's help text in the dictionary.

function SandboxForm({
  value,
  onChange,
  persisted,
}: {
  value: SandboxSetting;
  onChange: (v: SettingValue) => void;
  persisted: SandboxSetting | null;
}) {
  const set = patcher(value, onChange);
  const t = useT();
  return (
    <>
      <TextField label={t("config.sandboxSetting.registry.label")} value={value.agentRegistry} onChange={(v) => set({ agentRegistry: v })}
        mono testId="setting-sandbox-registry"
        help={t("config.sandboxSetting.registry.help")} />
      <TextField label={t("config.sandboxSetting.version.label")} value={value.agentVersion} onChange={(v) => set({ agentVersion: v })}
        mono placeholder="0.48.0" testId="setting-sandbox-version"
        help={t("config.sandboxSetting.version.help")} />
      {/* p0495: the pair is one rule, said once, above the two fields it governs. */}
      <p className="help" data-testid="setting-sandbox-timeouts-note">
        {t("config.sandboxSetting.timeoutsNote")}
      </p>
      <NumberField label={t("config.sandboxSetting.step.label")} value={value.stepTimeoutSeconds}
        onChange={(v) => set({ stepTimeoutSeconds: keep(v, value.stepTimeoutSeconds) })} testId="setting-sandbox-step"
        help={t("config.sandboxSetting.step.help")} />
      <NumberField label={t("config.sandboxSetting.runCommand.label")} value={value.runCommandTimeoutSeconds}
        onChange={(v) => set({ runCommandTimeoutSeconds: keep(v, value.runCommandTimeoutSeconds) })} testId="setting-sandbox-runcmd"
        help={t("config.sandboxSetting.runCommand.help")} />
      {/* 2026-09-18-0f27: the one sandbox setting that is live, and the two backends
          it does nothing for. Said next to the field, because "everything here needs
          a restart" stopped being true of this form the moment this field arrived. */}
      <p className="help" data-testid="setting-sandbox-bound-note">
        {t("config.sandboxSetting.boundNote")}
      </p>
      <NumberField label={t("config.sandboxSetting.maxConcurrent.label")} value={value.maxConcurrentSandboxes ?? undefined}
        onChange={(v) => set({ maxConcurrentSandboxes: v ?? null })} testId="setting-sandbox-maxconcurrent"
        placeholder="2"
        help={t("config.sandboxSetting.maxConcurrent.help")} />
      {/* 2026-09-22-2d11a: a design conversation keeps its source sandboxes between
          turns. A hold never denies a run — every capacity door releases what this
          server holds before it probes — so the cost of a wrong number is idle
          resources, not a refusal. */}
      <p className="help" data-testid="setting-sandbox-hold-note">
        {t("config.sandboxSetting.holdNote")}
      </p>
      <NumberField label={t("config.sandbox.hold.label")} value={value.holdSeconds ?? undefined}
        onChange={(v) => set({ holdSeconds: v ?? null })} testId="setting-sandbox-hold"
        placeholder="180"
        help={t("config.sandboxSetting.hold.help")} />
      {/* 2026-10-25-ff3b: hardening is the installation's choice; a project may opt back to root. */}
      <p className="help" data-testid="setting-sandbox-hardening-note">
        {t("config.sandboxSetting.hardeningNote")}
      </p>
      <CheckField label={t("config.sandboxSetting.harden")} value={value.hardeningEnabled === true}
        onChange={(v) => set({ hardeningEnabled: v })} testId="setting-sandbox-hardening" />
      <p className="help">{t("config.sandboxSetting.afterRestart")}</p>
      <SandboxImageTrustFields value={value} onChange={set} persisted={persisted} />
    </>
  );
}

function DeploymentForm({
  value,
  onChange,
}: {
  value: DeploymentSetting;
  onChange: (v: SettingValue) => void;
}) {
  const set = patcher(value, onChange);
  const t = useT();
  return (
    <>
      <TextField label={t("config.deploymentSetting.registry.label")} value={value.registry} onChange={(v) => set({ registry: v })} mono
        testId="setting-deployment-registry"
        help={t("config.deploymentSetting.registry.help")} />
      <TextField label={t("config.deploymentSetting.version.label")} value={value.version} onChange={(v) => set({ version: v })} mono
        testId="setting-deployment-version" help={t("config.deploymentSetting.version.help")} />
    </>
  );
}

function PrimaryProviderForm({
  value,
  onChange,
  capabilities,
}: {
  value: PrimaryProviderSetting;
  onChange: (v: SettingValue) => void;
  capabilities: ConfigCapabilities | null;
}) {
  const providers = capabilities?.agentProviders ?? [];
  const t = useT();
  return (
    <div className="field">
      <label>
        {t("config.setting.primaryProvider")}
        <span className="help">{t("config.setting.primaryProvider.sub")}</span>
      </label>
      <select
        data-testid="setting-primary-provider"
        className="mono"
        value={value.value ?? ""}
        onChange={(e) => onChange({ value: e.target.value === "" ? null : e.target.value })}
      >
        <option value="">{t("config.none")}</option>
        {value.value && !providers.includes(value.value) && (
          <option value={value.value}>{value.value}</option>
        )}
        {providers.map((p) => (
          <option key={p} value={p}>
            {p}
          </option>
        ))}
      </select>
    </div>
  );
}

function LimitsForm({ value, onChange }: { value: LimitsSetting; onChange: (v: SettingValue) => void }) {
  const set = patcher(value, onChange);
  const t = useT();
  const field = (
    key: keyof LimitsSetting,
    label: string,
    help?: string,
  ) => (
    <NumberField
      label={label}
      value={value[key]}
      onChange={(v) => set({ [key]: keep(v, value[key]) } as Partial<LimitsSetting>)}
      testId={`setting-limits-${key}`}
      help={help}
    />
  );
  // 2026-11-17-274cb: the five limits something reads — the scout call's tool-loop and time
  // bounds, and the sub-agent fan-out caps. The six the skill-call runtime read are retired.
  return (
    <>
      <DrawerSection title={t("config.limits.scout")} defaultOpen testId="setting-limits-calls">
        {field("maxToolCallsPerSkill", t("config.limits.maxToolCalls"))}
        {field("maxSecondsPerSkillCall", t("config.limits.maxSeconds"))}
      </DrawerSection>

      <DrawerSection title={t("config.limits.subAgents")} defaultOpen testId="setting-limits-subagents">
        {field("maxConcurrentSubAgents", t("config.limits.maxConcurrentSubAgents"))}
        {field("maxSubAgentsPerRun", t("config.limits.maxSubAgentsPerRun"))}
        {field("maxSubAgentsPerDialogTurn", t("config.limits.maxSubAgentsPerTurn"))}
      </DrawerSection>
    </>
  );
}

function CostCapValuesFields({
  value,
  onChange,
  testId,
}: {
  value: CostCapValues;
  onChange: (v: CostCapValues) => void;
  testId: string;
}) {
  const t = useT();
  return (
    <div className="fields">
      <NumberField label={t("config.costCap.usd")} value={value.usd} onChange={(v) => onChange({ ...value, usd: keep(v, value.usd) })}
        testId={`${testId}-usd`} />
      <NumberField label={t("config.costCap.tokens")} value={value.tokens} onChange={(v) => onChange({ ...value, tokens: keep(v, value.tokens) })}
        testId={`${testId}-tokens`} />
    </div>
  );
}

/** 2026-10-14-dbced: a tier left out of the stored perTier keeps its own code default, which
 *  the server's descriptor carries (an older server sends none, so Default stands in). The
 *  field shows that ceiling marked default; editing it stores only this tier. */
function TierCapField({
  tier,
  value,
  capabilities,
  onChange,
}: {
  tier: string;
  value: PipelineCostCapSetting;
  capabilities: ConfigCapabilities | null;
  onChange: (v: CostCapValues) => void;
}) {
  const t = useT();
  const stored = value.perTier[tier];
  const shown = stored ?? capabilities?.tierCostCapDefaults?.[tier] ?? value.default;
  return (
    <div className="field" data-testid={`setting-costcap-tier-${tier}`}>
      <label>{tier}</label>
      <CostCapValuesFields value={shown} onChange={onChange} testId={`setting-costcap-tier-${tier}`} />
      {!stored && (
        <span className="help" data-testid={`setting-costcap-tier-${tier}-default`}>
          {t("config.costCap.tierDefault", { usd: value.default.usd, tokens: value.default.tokens })}
        </span>
      )}
    </div>
  );
}

function PipelineCostCapForm({
  value,
  onChange,
  capabilities,
}: {
  value: PipelineCostCapSetting;
  onChange: (v: SettingValue) => void;
  capabilities: ConfigCapabilities | null;
}) {
  const set = patcher(value, onChange);
  const t = useT();
  const pipelines = capabilities?.pipelines ?? [];
  const perPipelineKeys = Object.keys(value.perPipeline);
  const addablePipelines = pipelines.filter((p) => !perPipelineKeys.includes(p));

  const setPerPipeline = (name: string, cap: CostCapValues) =>
    set({ perPipeline: { ...value.perPipeline, [name]: cap } });
  const removePerPipeline = (name: string) => {
    const next = { ...value.perPipeline };
    delete next[name];
    set({ perPipeline: next });
  };
  const setPerTier = (tier: string, cap: CostCapValues) =>
    set({ perTier: { ...value.perTier, [tier]: cap } });

  return (
    <>
      <DrawerSection title={t("config.costCap.default")} defaultOpen testId="setting-costcap-default">
        <CostCapValuesFields value={value.default} onChange={(v) => set({ default: v })}
          testId="setting-costcap-default" />
      </DrawerSection>

      <DrawerSection title={t("config.costCap.perTier")} summary={t("config.costCap.tierCount", { count: TIER_ORDER.length })} defaultOpen
        testId="setting-costcap-tiers">
        {TIER_ORDER.map((tier) => (
          <TierCapField key={tier} tier={tier} value={value} capabilities={capabilities}
            onChange={(v) => setPerTier(tier, v)} />
        ))}
      </DrawerSection>

      <DrawerSection title={t("config.costCap.perPipeline")} summary={t("config.costCap.setCount", { count: perPipelineKeys.length })}
        testId="setting-costcap-pipelines">
        {perPipelineKeys.length === 0 && <span className="help">{t("config.costCap.noOverrides")}</span>}
        {perPipelineKeys.map((name) => (
          <div key={name} className="field" data-testid={`setting-costcap-pipeline-${name}`}>
            <label>
              {name}
              <button type="button" className="help" onClick={() => removePerPipeline(name)}
                data-testid={`setting-costcap-pipeline-${name}-remove`}
                style={{ marginLeft: 8, color: "var(--bad)", cursor: "pointer" }}>
                {t("config.costCap.remove")}
              </button>
            </label>
            <CostCapValuesFields value={value.perPipeline[name]} onChange={(v) => setPerPipeline(name, v)}
              testId={`setting-costcap-pipeline-${name}`} />
          </div>
        ))}
        {addablePipelines.length > 0 && (
          <div className="field">
            <label>{t("config.costCap.add")}</label>
            <select
              className="mono"
              data-testid="setting-costcap-pipeline-add"
              value=""
              onChange={(e) => {
                if (e.target.value) setPerPipeline(e.target.value, value.default);
              }}
            >
              <option value="">{t("config.costCap.pickPipeline")}</option>
              {addablePipelines.map((p) => (
                <option key={p} value={p}>
                  {p}
                </option>
              ))}
            </select>
          </div>
        )}
      </DrawerSection>
    </>
  );
}

function QueueForm({ value, onChange }: { value: QueueSetting; onChange: (v: SettingValue) => void }) {
  const set = patcher(value, onChange);
  const t = useT();
  return (
    <>
      <NumberField label={t("config.queue.parallel.label")} value={value.maxParallelJobs}
        onChange={(v) => set({ maxParallelJobs: keep(v, value.maxParallelJobs) })} testId="setting-queue-parallel"
        help={t("config.queue.parallel.help")} />
      <NumberField label={t("config.queue.shutdown")} value={value.shutdownGraceSeconds}
        onChange={(v) => set({ shutdownGraceSeconds: keep(v, value.shutdownGraceSeconds) })} testId="setting-queue-shutdown" />
      <NumberField label={t("config.queue.retry.label")} value={value.redisRetryIntervalSeconds}
        onChange={(v) => set({ redisRetryIntervalSeconds: keep(v, value.redisRetryIntervalSeconds) })} testId="setting-queue-retry"
        help={t("config.queue.retry.help")} />
    </>
  );
}

function DialogueForm({ value, onChange }: { value: DialogueSetting; onChange: (v: SettingValue) => void }) {
  const set = patcher(value, onChange);
  const t = useT();
  return (
    <>
      <NumberField label={t("config.dialogue.hotWait.label")} value={value.hotWaitSeconds}
        onChange={(v) => set({ hotWaitSeconds: keep(v, value.hotWaitSeconds) })} testId="setting-dialogue-hotwait"
        help={t("config.dialogue.hotWait.help")} />
      <NumberField label={t("config.dialogue.approval.label")} value={value.approvalTimeoutSeconds}
        onChange={(v) => set({ approvalTimeoutSeconds: keep(v, value.approvalTimeoutSeconds) })} testId="setting-dialogue-approval"
        help={t("config.dialogue.approval.help")} />
      <TextField label={t("config.dialogue.dashboardUrl.label")} value={value.dashboardUrl ?? ""} onChange={(v) => set({ dashboardUrl: v || null })}
        mono testId="setting-dialogue-dashboard-url" help={t("config.dialogue.dashboardUrl.help")} />
      <TextField label={t("config.dialogue.adminAgent.label")} value={value.adminAgent ?? ""} onChange={(v) => set({ adminAgent: v || null })}
        mono testId="setting-dialogue-admin-agent" help={t("config.dialogue.adminAgent.help")} />
      <NumberField label={t("config.dialogue.adminReadLimit.label")} value={value.adminReadLimit ?? 50}
        onChange={(v) => set({ adminReadLimit: keep(v, value.adminReadLimit ?? 50) })} testId="setting-dialogue-admin-read-limit"
        help={t("config.dialogue.adminReadLimit.help")} />
      <NumberField label={t("config.dialogue.adminResult.label")} value={value.adminResultMaxChars ?? 40000}
        onChange={(v) => set({ adminResultMaxChars: keep(v, value.adminResultMaxChars ?? 40000) })} testId="setting-dialogue-admin-result-chars"
        help={t("config.dialogue.adminResult.help")} />
      <NumberField label={t("config.dialogue.mockSize.label")} value={value.mockMaxChars}
        onChange={(v) => set({ mockMaxChars: keep(v, value.mockMaxChars) })} testId="setting-dialogue-mockchars"
        help={t("config.dialogue.mockSize.help")} />
      <NumberField label={t("config.dialogue.mockPerSpec")} value={value.mockVersionsPerSpec}
        onChange={(v) => set({ mockVersionsPerSpec: keep(v, value.mockVersionsPerSpec) })} testId="setting-dialogue-mockspec" />
      <NumberField label={t("config.dialogue.mockPerConversation")} value={value.mockVersionsPerConversation}
        onChange={(v) => set({ mockVersionsPerConversation: keep(v, value.mockVersionsPerConversation) })}
        testId="setting-dialogue-mockconversation" />
      <NumberField label={t("config.dialogue.seriesBudget.label")} value={value.seriesMasterBudgetUsd}
        onChange={(v) => set({ seriesMasterBudgetUsd: keep(v, value.seriesMasterBudgetUsd) })} testId="setting-dialogue-seriesbudget"
        help={t("config.dialogue.seriesBudget.help")} />
      <NumberField label={t("config.dialogue.seriesBudgetAsk.label")} value={value.seriesMasterBudgetAskUsd ?? 2}
        onChange={(v) => set({ seriesMasterBudgetAskUsd: keep(v, value.seriesMasterBudgetAskUsd ?? 2) })}
        testId="setting-dialogue-seriesbudgetask" help={t("config.dialogue.seriesBudgetAsk.help")} />
      <NumberField label={t("config.dialogue.seriesThreadMax.label")} value={value.seriesMasterThreadMaxChars ?? 60000}
        onChange={(v) => set({ seriesMasterThreadMaxChars: keep(v, value.seriesMasterThreadMaxChars ?? 60000) })}
        testId="setting-dialogue-seriesthreadmax" help={t("config.dialogue.seriesThreadMax.help")} />
      <NumberField label={t("config.dialogue.seriesThreadKeep.label")} value={value.seriesMasterThreadKeepTurns ?? 4}
        onChange={(v) => set({ seriesMasterThreadKeepTurns: keep(v, value.seriesMasterThreadKeepTurns ?? 4) })}
        testId="setting-dialogue-seriesthreadkeep" help={t("config.dialogue.seriesThreadKeep.help")} />
      <NumberField label={t("config.dialogue.seriesSettle.label")} value={value.seriesWakeSettleSeconds ?? 60}
        onChange={(v) => set({ seriesWakeSettleSeconds: keep(v, value.seriesWakeSettleSeconds ?? 60) })}
        testId="setting-dialogue-seriessettle" help={t("config.dialogue.seriesSettle.help")} />
      <NumberField label={t("config.dialogue.seriesStatus.label")} value={value.seriesStatusMinutes}
        onChange={(v) => set({ seriesStatusMinutes: keep(v, value.seriesStatusMinutes) })} testId="setting-dialogue-seriesstatus"
        help={t("config.dialogue.seriesStatus.help")} />
      {/* 2026-11-28-d1ffb: whether a recommended series question is taken by default at all; absent reads as on. */}
      <CheckField label={t("config.dialogue.questionDefaultEnabled.label")} value={value.questionDefaultEnabled !== false}
        onChange={(v) => set({ questionDefaultEnabled: v })} testId="setting-dialogue-questiondefault-enabled"
        help={t("config.dialogue.questionDefaultEnabled.help")} />
      <NumberField label={t("config.dialogue.questionDefault.label")} value={value.questionDefaultMinutes}
        onChange={(v) => set({ questionDefaultMinutes: keep(v, value.questionDefaultMinutes) })} testId="setting-dialogue-questiondefault"
        help={t("config.dialogue.questionDefault.help")} />
    </>
  );
}

function SkillsForm({ value, onChange }: { value: SkillsSetting; onChange: (v: SettingValue) => void }) {
  const set = patcher(value, onChange);
  const t = useT();
  return (
    <>
      <div className="field">
        <label>
          {t("config.skills.source.label")}
          <span className="help">{t("config.skills.source.help")}</span>
        </label>
        <select
          className="mono"
          data-testid="setting-skills-source"
          value={value.source}
          onChange={(e) => set({ source: Number(e.target.value) })}
        >
          {SKILLS_SOURCES.map((s) => (
            <option key={s.value} value={s.value}>
              {t(s.label)}
            </option>
          ))}
        </select>
      </div>
      <TextField label={t("config.skills.version.label")} value={value.version ?? ""} onChange={(v) => set({ version: v || null })}
        mono testId="setting-skills-version" help={t("config.skills.version.help")} />
      <TextField label={t("config.skills.path.label")} value={value.path ?? ""} onChange={(v) => set({ path: v || null })} mono
        testId="setting-skills-path" help={t("config.skills.path.help")} />
      <TextField label={t("config.skills.url.label")} value={value.url ?? ""} onChange={(v) => set({ url: v || null })} mono
        testId="setting-skills-url" help={t("config.skills.url.help")} />
      <TextField label={t("config.skills.sha.label")} value={value.sha256 ?? ""} onChange={(v) => set({ sha256: v || null })} mono
        testId="setting-skills-sha256" help={t("config.skills.sha.help")} />
      <TextField label={t("config.skills.cacheDir.label")} value={value.cacheDir} onChange={(v) => set({ cacheDir: v })} mono
        testId="setting-skills-cachedir" help={t("config.skills.cacheDir.help")} />
    </>
  );
}

function PipelineDataFlowForm({
  value,
  onChange,
}: {
  value: PipelineDataFlowSetting;
  onChange: (v: SettingValue) => void;
}) {
  const set = patcher(value, onChange);
  const t = useT();
  return (
    <CheckField label={t("config.dataFlow.enforce")} value={value.enforce}
      onChange={(v) => set({ enforce: v })} testId="setting-dataflow-enforce" />
  );
}

function TraceForm({
  value,
  onChange,
}: {
  value: TraceSetting;
  onChange: (v: SettingValue) => void;
}) {
  const set = patcher(value, onChange);
  const t = useT();
  return (
    <CheckField label={t("config.trace.enabled")}
      value={value.enabled} onChange={(v) => set({ enabled: v })} testId="setting-trace-enabled" />
  );
}
