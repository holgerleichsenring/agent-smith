"use client";

import { useEffect, useState } from "react";
import { fetchConfig, type ConfigProject, type ConfigSnapshot } from "@/lib/configApi";
import { WiringChip } from "@/components/config/primitives";
import { ProjectSelect } from "./ProjectSelect";
import { ProjectDetailPanel } from "./ProjectDetailPanel";
import { SubsystemDetail } from "./SubsystemDetail";
import type { SubsystemActivity } from "@/hooks/useSubsystemActivity";

// p0271: the System → Config view; p0343d: parity re-dress.
// p0345c: the page now answers "is what RUNS what you CONFIGURED":
//   1. a DRIFT banner when agentsmith.yml changed after the runtime's last read
//   2. per-project WIRING chips (the studio's .wire vocabulary) with a
//      per-setting provenance summary (explicit vs default vs per-run) and a
//      "read Xs ago" freshness line
//   3. the resolved-config sheet (per-setting provenance badges live there)
//   4. the raw read-events stream, collapsed at the bottom
// The snapshot stays a redacted allow-list (no secret ever reaches the page).

export function ConfigView({ activity }: { activity: SubsystemActivity }) {
  const [config, setConfig] = useState<ConfigSnapshot | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [selected, setSelected] = useState<string>("");
  const [streamOpen, setStreamOpen] = useState(false);

  useEffect(() => {
    const controller = new AbortController();
    fetchConfig(controller.signal)
      .then((snapshot) => {
        setConfig(snapshot);
        setSelected(snapshot.projects[0]?.name ?? "");
      })
      .catch((e: Error) => {
        if (e.name !== "AbortError") setError(e.message);
      });
    return () => controller.abort();
  }, []);

  const selectedProject = config?.projects.find((p) => p.name === selected) ?? null;
  const drift = hasDrift(config);

  return (
    <div data-testid="config-view">
      {drift && config && (
        <div className="banner wait" data-testid="config-drift-banner">
          <div className="b-ic" aria-hidden>
            !
          </div>
          <div className="b-body">
            <div className="b-title">
              agentsmith.yml changed after the last read — the runtime may be stale
            </div>
            <div className="b-sub mono">
              {config.configPath ?? "agentsmith.yml"} · modified {fmtInstant(config.fileModifiedAt)} · last
              read {fmtInstant(config.lastReadAt)}
            </div>
          </div>
        </div>
      )}

      <section>
        <div className="section-head">
          <h2>Wiring</h2>
          {config && config.projects.length > 0 && <span className="cnt">{config.projects.length}</span>}
          <span className="sh-sub mono" data-testid="config-read-freshness">
            {freshnessLine(config)}
          </span>
        </div>

        {error ? (
          <div className="stateline err" data-testid="config-view-error">
            Failed to load config: {error}
          </div>
        ) : !config ? (
          <div className="stateline" data-testid="config-view-loading">
            Loading config…
          </div>
        ) : config.projects.length === 0 ? (
          <div className="stateline" data-testid="config-view-empty">
            No projects configured.
          </div>
        ) : (
          <div className="wires" style={{ marginTop: 14 }}>
            {config.projects.map((p) => (
              <ProjectWireRow key={p.name} project={p} />
            ))}
          </div>
        )}
      </section>

      {config && config.projects.length > 0 && !error && (
        <section>
          <div className="section-head">
            <h2>Resolved configuration</h2>
            <span className="sh-sub">pick a project — everything reachable around it</span>
          </div>
          <div style={{ margin: "14px 0" }}>
            <ProjectSelect projects={config.projects} selected={selected} onSelect={setSelected} />
          </div>
          {selectedProject && (
            <ProjectDetailPanel
              project={selectedProject}
              repos={config.repos}
              trackers={config.trackers}
              agents={config.agents}
            />
          )}
        </section>
      )}

      <section>
        <div className="section-head">
          <h2>Raw read events</h2>
          <button
            type="button"
            className="sh-toggle"
            data-testid="config-stream-toggle"
            aria-expanded={streamOpen}
            onClick={() => setStreamOpen((o) => !o)}
          >
            {streamOpen ? "hide ▴" : "show ▾"}
          </button>
        </div>
        {streamOpen && <SubsystemDetail activity={activity} heading="Event stream" />}
      </section>
    </div>
  );
}

// One project's wiring as the studio's .wire row — agent → [project] ← tracker
// · repos — plus the provenance summary of its resolved settings.
function ProjectWireRow({ project }: { project: ConfigProject }) {
  const provenance = provenanceSummary(project);
  return (
    <div className="wire" data-testid={`config-wiring-${project.name}`}>
      <span className="wlbl">wires</span>
      <WiringChip label="agent" kind="agent" value={project.agentName} resolved testId={`config-wiring-agent-${project.name}`} />
      <span className="warr">→</span>
      <span className="pw-node proj">{project.name}</span>
      <span className="warr">←</span>
      <WiringChip label="tracker" kind="tracker" value={project.trackerName} resolved testId={`config-wiring-tracker-${project.name}`} />
      <span className="warr">·</span>
      {project.repoNames.length === 0 && <span className="wlbl">no repos</span>}
      {project.repoNames.map((name) => (
        <WiringChip key={name} label="repo" kind="repo" value={name} resolved testId={`config-wiring-repo-${project.name}-${name}`} />
      ))}
      <span className="warr">·</span>
      <span className="wchip" data-testid={`config-provenance-${project.name}`}>
        <span className="wd" />
        {provenance}
      </span>
    </div>
  );
}

// p0345c: how many of this project's resolved settings are explicit overrides
// vs global defaults vs resolved per run — the at-a-glance provenance story
// (the sheet below carries the per-setting badges).
function provenanceSummary(project: ConfigProject): string {
  const sources = Object.values(project.resolved ?? {})
    .filter((v): v is { source: string } => !!v && typeof v === "object" && "source" in (v as object))
    .map((v) => v.source);
  const explicit = sources.filter((s) => s === "override").length;
  const defaults = sources.filter((s) => s === "global-default").length;
  const perRun = sources.filter((s) => s === "run-resolved").length;
  const parts = [`${explicit} explicit`, `${defaults} default`];
  if (perRun > 0) parts.push(`${perRun} per-run`);
  return parts.join(" · ");
}

function hasDrift(config: ConfigSnapshot | null): boolean {
  if (!config?.fileModifiedAt || !config.lastReadAt) return false;
  return new Date(config.fileModifiedAt).getTime() > new Date(config.lastReadAt).getTime();
}

// "read 42s ago" / "read 3m ago" — the freshness of the runtime's last actual
// config read; honest when no read was recorded (yet).
function freshnessLine(config: ConfigSnapshot | null): string {
  if (!config) return "";
  if (!config.lastReadAt) return "no config read recorded";
  const seconds = Math.max(0, Math.round((Date.now() - new Date(config.lastReadAt).getTime()) / 1000));
  if (seconds < 60) return `read ${seconds}s ago`;
  if (seconds < 3600) return `read ${Math.round(seconds / 60)}m ago`;
  return `read ${Math.round(seconds / 3600)}h ago`;
}

function fmtInstant(iso: string | null | undefined): string {
  return iso ? new Date(iso).toISOString().replace("T", " ").slice(0, 19) : "—";
}
