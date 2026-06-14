"use client";

import type { ReactNode } from "react";
import { Badge } from "@/components/ui/Badge";
import { Card } from "@/components/ui/Card";
import { SectionLabel } from "@/components/ui/SectionLabel";
import type {
  ConfigAgent,
  ConfigGlobals,
  ConfigRepo,
  ConfigSnapshot,
  ConfigTracker,
} from "@/lib/configApi";

// p0266: the structured detail beneath the config graph — the global defaults
// (sandbox / orchestrator / limits / cost-cap / persistence) and the redacted
// agent / repo / tracker entries. Prop-driven over the redacted ConfigSnapshot
// so it unit-tests without the live fetch. DESIGN.md primitives only
// (Card / SectionLabel / Badge), dsh-* tokens, no raw px.

export function ConfigDetail({ config }: { config: ConfigSnapshot }) {
  return (
    <div className="content-shell space-y-8" data-testid="config-detail">
      <GlobalsGrid globals={config.globals} />
      <Section title="Agents" testId="config-agents">
        {config.agents.map((a) => <AgentCard key={a.name} agent={a} />)}
      </Section>
      <Section title="Repositories" testId="config-repos">
        {config.repos.map((r) => <RepoCard key={r.name} repo={r} />)}
      </Section>
      <Section title="Trackers" testId="config-trackers">
        {config.trackers.map((t) => <TrackerCard key={t.name} tracker={t} />)}
      </Section>
    </div>
  );
}

function GlobalsGrid({ globals }: { globals: ConfigGlobals }) {
  // p0270b: the per-project RESOLVED values are the primary surface now; these
  // raw global defaults are demoted into a disclosure ("reference"), so they
  // stay available without competing with the effective-config explainer.
  return (
    <details data-testid="config-globals" className="group">
      <summary className="cursor-pointer list-none">
        <SectionLabel className="inline-flex items-center gap-1.5">
          <span className="dsh-mono text-stone-400 group-open:rotate-90 inline-block transition-transform">›</span>
          Global defaults (reference)
        </SectionLabel>
      </summary>
      <div className="mt-2 grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
        <KvCard title="Sandbox" testId="config-global-sandbox" rows={[
          ["registry", globals.sandbox.agentRegistry],
          ["image version", globals.sandbox.agentVersion || "—"],
          ["step timeout", `${globals.sandbox.stepTimeoutSeconds}s`],
          ["run_command timeout", `${globals.sandbox.runCommandTimeoutSeconds}s`],
        ]} />
        <KvCard title="Orchestrator" testId="config-global-orchestrator" rows={[
          ["registry", globals.orchestrator.registry || "—"],
          ["version", globals.orchestrator.version || "—"],
          ["max run wall-time", `${globals.orchestrator.maxRunWallTimeSeconds}s`],
        ]} />
        <KvCard title="Loop limits" testId="config-global-limits" rows={[
          ["tool calls / skill", globals.limits.maxToolCallsPerSkill],
          ["llm calls / skill", globals.limits.maxLlmCallsPerSkill],
          ["concurrent skill calls", globals.limits.maxConcurrentSkillCalls],
          ["sub-agents / run", globals.limits.maxSubAgentsPerRun],
        ]} />
        <KvCard title="Cost cap / run" testId="config-global-costcap" rows={[
          ["usd", `$${globals.costCap.usd}`],
          ["tokens", globals.costCap.tokens.toLocaleString()],
        ]} />
        <KvCard title="Persistence" testId="config-global-persistence" rows={[
          ["provider", globals.persistenceProvider],
        ]} />
      </div>
    </details>
  );
}

function KvCard({ title, rows, testId }: { title: string; rows: [string, ReactNode][]; testId: string }) {
  return (
    <Card data-testid={testId} className="px-5 py-4">
      <div className="dsh-body font-medium text-stone-700">{title}</div>
      <dl className="mt-2 space-y-1">
        {rows.map(([k, v]) => (
          <div key={k} className="flex items-baseline justify-between gap-3">
            <dt className="dsh-label text-stone-400">{k}</dt>
            <dd className="dsh-mono tabular-nums text-stone-700">{v}</dd>
          </div>
        ))}
      </dl>
    </Card>
  );
}

function Section({ title, testId, children }: { title: string; testId: string; children: ReactNode[] }) {
  if (children.length === 0) return null;
  return (
    <section data-testid={testId}>
      <SectionLabel>{title}</SectionLabel>
      <div className="mt-2 grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">{children}</div>
    </section>
  );
}

function AgentCard({ agent }: { agent: ConfigAgent }) {
  return (
    <Card data-testid={`config-agent-${agent.name}`} className="px-5 py-4">
      <div className="flex items-center gap-2">
        <span className="dsh-body font-medium text-stone-700">{agent.name}</span>
        <Badge tone="neutral">{agent.type}</Badge>
      </div>
      <div className="mt-1 dsh-mono text-stone-500">{agent.model}</div>
      <div className="mt-2 flex flex-wrap gap-1.5">
        <Badge tone="neutral">timeout {agent.networkTimeoutSeconds}s</Badge>
        <Badge tone="neutral">fix×{agent.maxFixIterations}</Badge>
        {agent.requestsPerMinute != null && <Badge tone="neutral">{agent.requestsPerMinute} rpm</Badge>}
      </div>
    </Card>
  );
}

function RepoCard({ repo }: { repo: ConfigRepo }) {
  return (
    <Card data-testid={`config-repo-${repo.name}`} className="px-5 py-4">
      <div className="flex items-center gap-2">
        <span className="dsh-body font-medium text-stone-700">{repo.name}</span>
        <Badge tone="neutral">{repo.type}</Badge>
      </div>
      <div className="mt-1 dsh-mono text-stone-500">{repo.host ?? "local"}</div>
      {repo.defaultBranch && <div className="mt-1 dsh-label text-stone-400">branch: {repo.defaultBranch}</div>}
    </Card>
  );
}

function TrackerCard({ tracker }: { tracker: ConfigTracker }) {
  return (
    <Card data-testid={`config-tracker-${tracker.name}`} className="px-5 py-4">
      <div className="flex items-center gap-2">
        <span className="dsh-body font-medium text-stone-700">{tracker.name}</span>
        <Badge tone="neutral">{tracker.type}</Badge>
      </div>
      {tracker.project && <div className="mt-1 dsh-mono text-stone-500">{tracker.project}</div>}
      <div className="mt-2 flex flex-wrap gap-1.5">
        {tracker.openStates.map((s) => <Badge key={s} tone="amber">{s}</Badge>)}
        {tracker.doneStatus && <Badge tone="green">{tracker.doneStatus}</Badge>}
      </div>
    </Card>
  );
}
