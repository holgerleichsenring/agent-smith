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
  PipelineStorageSetting,
  PrimaryProviderSetting,
  QueueSetting,
  RegistriesSetting,
  SandboxSetting,
  SettingKey,
  SettingShapes,
  SettingValue,
  SkillsSetting,
} from "@/lib/configApi";
import { CheckField, DrawerSection, NumberField, TextField } from "./formFields";

// p0353: the per-settings typed forms. One case per settings key — flat docs are a
// straightforward field list; the nested pipeline-cost-cap (default + per-pipeline +
// per-tier) and the registries LIST render as structured sub-forms. Every field is
// controlled: the parent owns the draft, the form patches it. A required number that
// is momentarily cleared retains its loaded value (via `keep`) instead of drafting a
// silent 0 — the backend validates the shape.

const TIER_ORDER = ["Trivial", "Small", "Medium", "Large"] as const;

const SKILLS_SOURCES = [
  { value: 0, label: "Default (release tag)" },
  { value: 1, label: "Path (pre-mounted directory)" },
  { value: 2, label: "Url (explicit tarball)" },
  { value: 3, label: "Embedded (bundled in the binary)" },
];

export function SettingsForm({
  settingKey,
  value,
  onChange,
  capabilities,
}: {
  settingKey: SettingKey;
  value: SettingValue;
  onChange: (v: SettingValue) => void;
  capabilities: ConfigCapabilities | null;
}) {
  switch (settingKey) {
    case "orchestrator":
      return <OrchestratorForm value={value as OrchestratorSetting} onChange={onChange} />;
    case "sandbox":
      return <SandboxForm value={value as SandboxSetting} onChange={onChange} />;
    case "deployment":
      return <DeploymentForm value={value as DeploymentSetting} onChange={onChange} />;
    case "registries":
      return <RegistriesForm value={value as RegistriesSetting} onChange={onChange} />;
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
    case "pipeline_storage":
      return <PipelineStorageForm value={value as PipelineStorageSetting} onChange={onChange} />;
    case "pipeline_data_flow":
      return <PipelineDataFlowForm value={value as PipelineDataFlowSetting} onChange={onChange} />;
  }
}

// A patcher bound to a shape — merges a partial into the current value.
function patcher<T extends SettingShapes[SettingKey]>(value: T, onChange: (v: SettingValue) => void) {
  return (patch: Partial<T>) => onChange({ ...value, ...patch } as SettingValue);
}

// A required number that is momentarily emptied keeps its current value rather than
// collapsing to 0; the operator select-all-retypes and the loaded value stays visible.
function keep(v: number | undefined, current: number): number {
  return v ?? current;
}

function OrchestratorForm({
  value,
  onChange,
}: {
  value: OrchestratorSetting;
  onChange: (v: SettingValue) => void;
}) {
  const set = patcher(value, onChange);
  return (
    <>
      <TextField label="Registry" value={value.registry} onChange={(v) => set({ registry: v })} mono
        placeholder="ghcr.io/your-org" testId="setting-orchestrator-registry"
        help="the registry the orchestrator image is pulled from" />
      <TextField label="Version" value={value.version} onChange={(v) => set({ version: v })} mono
        placeholder="0.49.0" testId="setting-orchestrator-version" help="orchestrator image tag" />
      <NumberField label="Max run wall-time (seconds)" value={value.maxRunWallTimeSeconds}
        onChange={(v) => set({ maxRunWallTimeSeconds: keep(v, value.maxRunWallTimeSeconds) })} testId="setting-orchestrator-walltime"
        help="a run older than this is cancelled by the watchdog" />
    </>
  );
}

function SandboxForm({ value, onChange }: { value: SandboxSetting; onChange: (v: SettingValue) => void }) {
  const set = patcher(value, onChange);
  return (
    <>
      <TextField label="Agent registry" value={value.agentRegistry} onChange={(v) => set({ agentRegistry: v })}
        mono testId="setting-sandbox-registry" help="the registry the sandbox agent image is pulled from" />
      <TextField label="Agent version" value={value.agentVersion} onChange={(v) => set({ agentVersion: v })}
        mono placeholder="0.48.0" testId="setting-sandbox-version" help="sandbox agent image tag" />
      <NumberField label="Step timeout (seconds)" value={value.stepTimeoutSeconds}
        onChange={(v) => set({ stepTimeoutSeconds: keep(v, value.stepTimeoutSeconds) })} testId="setting-sandbox-step"
        help="per-sandbox-step wall-time cap" />
      <NumberField label="Run-command timeout (seconds)" value={value.runCommandTimeoutSeconds}
        onChange={(v) => set({ runCommandTimeoutSeconds: keep(v, value.runCommandTimeoutSeconds) })} testId="setting-sandbox-runcmd"
        help="default timeout for an agent run_command; bounded by the step cap" />
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
  return (
    <>
      <TextField label="Registry" value={value.registry} onChange={(v) => set({ registry: v })} mono
        testId="setting-deployment-registry"
        help="the one-knob base registry for both images when their own field is unset" />
      <TextField label="Version" value={value.version} onChange={(v) => set({ version: v })} mono
        testId="setting-deployment-version" help="the one-knob base version applied where unset" />
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
  return (
    <div className="field">
      <label>
        Primary provider
        <span className="help">the default agent provider when a project names none</span>
      </label>
      <select
        data-testid="setting-primary-provider"
        className="mono"
        value={value.value ?? ""}
        onChange={(e) => onChange({ value: e.target.value === "" ? null : e.target.value })}
      >
        <option value="">— none —</option>
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
  // The 11 flat limits grouped by domain: per-skill call counts, the per-call token/
  // time budgets, and the sub-agent fan-out caps.
  return (
    <>
      <DrawerSection title="Call caps" defaultOpen testId="setting-limits-calls">
        {field("maxToolCallsPerSkill", "Max tool calls per skill")}
        {field("maxToolCallsPerInvestigator", "Max tool calls per investigator")}
        {field("maxToolCallsPerVerifier", "Max tool calls per verifier")}
        {field("maxLlmCallsPerSkill", "Max LLM calls per skill")}
        {field("maxConcurrentSkillCalls", "Max concurrent skill calls")}
        {field("maxSkillsPerPhase", "Max skills per phase")}
      </DrawerSection>

      <DrawerSection title="Token & time budgets" defaultOpen testId="setting-limits-budgets">
        {field("maxInputTokensPerSkillCall", "Max input tokens per skill call")}
        {field("maxOutputTokensPerSkillCall", "Max output tokens per skill call")}
        {field("maxSecondsPerSkillCall", "Max seconds per skill call")}
      </DrawerSection>

      <DrawerSection title="Sub-agent caps" defaultOpen testId="setting-limits-subagents">
        {field("maxConcurrentSubAgents", "Max concurrent sub-agents")}
        {field("maxSubAgentsPerRun", "Max sub-agents per run")}
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
  return (
    <div className="fields">
      <NumberField label="USD" value={value.usd} onChange={(v) => onChange({ ...value, usd: keep(v, value.usd) })}
        testId={`${testId}-usd`} />
      <NumberField label="Tokens" value={value.tokens} onChange={(v) => onChange({ ...value, tokens: keep(v, value.tokens) })}
        testId={`${testId}-tokens`} />
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
      <DrawerSection title="Default cap" defaultOpen testId="setting-costcap-default">
        <CostCapValuesFields value={value.default} onChange={(v) => set({ default: v })}
          testId="setting-costcap-default" />
      </DrawerSection>

      <DrawerSection title="Per-tier caps" summary={`${TIER_ORDER.length} tiers`} defaultOpen
        testId="setting-costcap-tiers">
        {TIER_ORDER.map((tier) => (
          <div key={tier} className="field" data-testid={`setting-costcap-tier-${tier}`}>
            <label>{tier}</label>
            <CostCapValuesFields
              value={value.perTier[tier] ?? value.default}
              onChange={(v) => setPerTier(tier, v)}
              testId={`setting-costcap-tier-${tier}`}
            />
          </div>
        ))}
      </DrawerSection>

      <DrawerSection title="Per-pipeline overrides" summary={`${perPipelineKeys.length} set`}
        testId="setting-costcap-pipelines">
        {perPipelineKeys.length === 0 && <span className="help">no per-pipeline overrides</span>}
        {perPipelineKeys.map((name) => (
          <div key={name} className="field" data-testid={`setting-costcap-pipeline-${name}`}>
            <label>
              {name}
              <button type="button" className="help" onClick={() => removePerPipeline(name)}
                data-testid={`setting-costcap-pipeline-${name}-remove`}
                style={{ marginLeft: 8, color: "var(--bad)", cursor: "pointer" }}>
                remove
              </button>
            </label>
            <CostCapValuesFields value={value.perPipeline[name]} onChange={(v) => setPerPipeline(name, v)}
              testId={`setting-costcap-pipeline-${name}`} />
          </div>
        ))}
        {addablePipelines.length > 0 && (
          <div className="field">
            <label>Add a pipeline override</label>
            <select
              className="mono"
              data-testid="setting-costcap-pipeline-add"
              value=""
              onChange={(e) => {
                if (e.target.value) setPerPipeline(e.target.value, value.default);
              }}
            >
              <option value="">— pick a pipeline —</option>
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
  return (
    <>
      <NumberField label="Max parallel jobs" value={value.maxParallelJobs}
        onChange={(v) => set({ maxParallelJobs: keep(v, value.maxParallelJobs) })} testId="setting-queue-parallel"
        help="the sole backpressure knob for downstream pipeline execution" />
      <NumberField label="Consume block (seconds)" value={value.consumeBlockSeconds}
        onChange={(v) => set({ consumeBlockSeconds: keep(v, value.consumeBlockSeconds) })} testId="setting-queue-block" />
      <NumberField label="Shutdown grace (seconds)" value={value.shutdownGraceSeconds}
        onChange={(v) => set({ shutdownGraceSeconds: keep(v, value.shutdownGraceSeconds) })} testId="setting-queue-shutdown" />
      <NumberField label="Redis retry interval (seconds)" value={value.redisRetryIntervalSeconds}
        onChange={(v) => set({ redisRetryIntervalSeconds: keep(v, value.redisRetryIntervalSeconds) })} testId="setting-queue-retry"
        help="poll cadence when Redis is configured but unreachable" />
    </>
  );
}

function DialogueForm({ value, onChange }: { value: DialogueSetting; onChange: (v: SettingValue) => void }) {
  const set = patcher(value, onChange);
  return (
    <>
      <NumberField label="Hot-wait window (seconds)" value={value.hotWaitSeconds}
        onChange={(v) => set({ hotWaitSeconds: keep(v, value.hotWaitSeconds) })} testId="setting-dialogue-hotwait"
        help="in-memory wait before an eligible run checkpoints and parks" />
      <NumberField label="Approval timeout (seconds)" value={value.approvalTimeoutSeconds}
        onChange={(v) => set({ approvalTimeoutSeconds: keep(v, value.approvalTimeoutSeconds) })} testId="setting-dialogue-approval"
        help="when it elapses on a parked run, the persisted default answer applies" />
    </>
  );
}

function SkillsForm({ value, onChange }: { value: SkillsSetting; onChange: (v: SettingValue) => void }) {
  const set = patcher(value, onChange);
  return (
    <>
      <div className="field">
        <label>
          Source
          <span className="help">how the skill catalog is resolved at boot</span>
        </label>
        <select
          className="mono"
          data-testid="setting-skills-source"
          value={value.source}
          onChange={(e) => set({ source: Number(e.target.value) })}
        >
          {SKILLS_SOURCES.map((s) => (
            <option key={s.value} value={s.value}>
              {s.label}
            </option>
          ))}
        </select>
      </div>
      <TextField label="Version" value={value.version ?? ""} onChange={(v) => set({ version: v || null })}
        mono testId="setting-skills-version" help="release tag for the default source" />
      <TextField label="Path" value={value.path ?? ""} onChange={(v) => set({ path: v || null })} mono
        testId="setting-skills-path" help="pre-mounted catalog directory for the path source" />
      <TextField label="Url" value={value.url ?? ""} onChange={(v) => set({ url: v || null })} mono
        testId="setting-skills-url" help="explicit tarball url for the url source" />
      <TextField label="SHA256" value={value.sha256 ?? ""} onChange={(v) => set({ sha256: v || null })} mono
        testId="setting-skills-sha256" help="optional tarball hash for a downloading source" />
      <TextField label="Cache directory" value={value.cacheDir} onChange={(v) => set({ cacheDir: v })} mono
        testId="setting-skills-cachedir" help="where downloaded catalogs are extracted (empty = default)" />
    </>
  );
}

function PipelineStorageForm({
  value,
  onChange,
}: {
  value: PipelineStorageSetting;
  onChange: (v: SettingValue) => void;
}) {
  const set = patcher(value, onChange);
  return (
    <NumberField label="Redis TTL (hours)" value={value.redisTtlHours}
      onChange={(v) => set({ redisTtlHours: keep(v, value.redisTtlHours) })} testId="setting-storage-ttl"
      help="safety-net TTL for abandoned in-flight runs" />
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
  return (
    <CheckField label="Enforce data-flow gating" value={value.enforce}
      onChange={(v) => set({ enforce: v })} testId="setting-dataflow-enforce" />
  );
}

function RegistriesForm({
  value,
  onChange,
}: {
  value: RegistriesSetting;
  onChange: (v: SettingValue) => void;
}) {
  const setAt = (i: number, patch: Partial<RegistriesSetting[number]>) =>
    onChange(value.map((r, j) => (j === i ? { ...r, ...patch } : r)) as SettingValue);
  const remove = (i: number) => onChange(value.filter((_, j) => j !== i) as SettingValue);
  const add = () => onChange([...value, { host: "", username: "", token: "" }] as SettingValue);

  return (
    <>
      {value.length === 0 && <span className="help">no private feeds configured</span>}
      {value.map((entry, i) => (
        <DrawerSection key={i} title={entry.host || `Feed ${i + 1}`} defaultOpen
          testId={`setting-registry-${i}`}>
          <TextField label="Host" value={entry.host} onChange={(v) => setAt(i, { host: v })} mono
            placeholder="pkgs.dev.azure.com" testId={`setting-registry-${i}-host`} />
          <TextField label="Username" value={entry.username} onChange={(v) => setAt(i, { username: v })} mono
            placeholder="any" testId={`setting-registry-${i}-username`} />
          <TextField label="Token" value={entry.token} onChange={(v) => setAt(i, { token: v })} mono
            placeholder="${feed_token}" testId={`setting-registry-${i}-token`}
            help="the secret reference for the feed token" />
          <button type="button" className="btn" onClick={() => remove(i)}
            data-testid={`setting-registry-${i}-remove`} style={{ color: "var(--bad)" }}>
            Remove feed
          </button>
        </DrawerSection>
      ))}
      <button type="button" className="btn" onClick={add} data-testid="setting-registry-add">
        Add a feed
      </button>
    </>
  );
}
