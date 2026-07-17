"use client";

import type { StudioProject } from "@/lib/configApi";
import { WiringChip } from "./primitives";
import { projectIntegrity } from "./integrity";
import type { ConfigCatalog } from "./useConfigCatalog";

// p0345: the live wiring preview for the project form — agent → project ← tracker
// with repos below — plus an integrity confirmation bar that turns green only
// when every reference resolves against the catalog. It recomputes on each
// keystroke/pick, so the operator sees the graph and its health as they build it.

export function ProjectWiring({
  project,
  catalog,
}: {
  project: StudioProject;
  catalog: ConfigCatalog;
}) {
  const integrity = projectIntegrity(catalog, project);
  return (
    <div className="flex flex-col gap-3" data-testid="project-wiring">
      <div className="eyebrow-uppercase text-stone-400">wiring preview</div>

      <div className="flex flex-wrap items-center justify-center gap-2 rounded-md border border-stone-200 bg-white px-3 py-4">
        <WiringChip
          label="agent"
          value={project.agent}
          resolved={integrity.agentOk}
          testId="wiring-agent"
        />
        <span className="text-stone-400">→</span>
        <span className="dsh-body font-mono font-semibold text-stone-900">
          {project.id || "(new project)"}
        </span>
        <span className="text-stone-400">←</span>
        <WiringChip
          label="tracker"
          value={project.tracker}
          resolved={integrity.trackerOk}
          testId="wiring-tracker"
        />
      </div>

      <div className="flex flex-wrap justify-center gap-2">
        {integrity.repoResults.length === 0 && (
          <span className="dsh-label text-stone-400">no repos selected</span>
        )}
        {/* p0345b: conn-scoped refs ("conn/Name") resolve via the connections
            catalog and are labeled as such; plain refs stay "repo". */}
        {integrity.repoResults.map((r) => (
          <WiringChip
            key={r.id}
            label={r.id.includes("/") ? "conn repo" : "repo"}
            value={r.id}
            resolved={r.ok}
            testId={`wiring-repo-${r.id}`}
          />
        ))}
      </div>

      <div
        data-testid="project-integrity"
        data-ok={integrity.ok ? "true" : "false"}
        className={
          integrity.ok
            ? "flex items-center gap-2 rounded-md border border-emerald-300 bg-emerald-50 px-3 py-2 dsh-body text-emerald-700"
            : "flex items-center gap-2 rounded-md border border-amber-300 bg-amber-50 px-3 py-2 dsh-body text-amber-700"
        }
      >
        <span
          className={`h-2 w-2 flex-none rounded-full ${integrity.ok ? "bg-emerald-500" : "bg-amber-500"}`}
          aria-hidden
        />
        {integrity.ok
          ? "All references resolve — integrity confirmed"
          : integrityHint(integrity)}
      </div>
    </div>
  );
}

function integrityHint(i: ReturnType<typeof projectIntegrity>): string {
  const missing: string[] = [];
  if (!i.agentOk) missing.push("agent");
  if (!i.trackerOk) missing.push("tracker");
  if (!i.reposOk) missing.push("at least one repo");
  const broken = i.repoResults.filter((r) => !r.ok).map((r) => r.id);
  if (broken.length) missing.push(`unknown repo/connection ${broken.join(", ")}`);
  return `Unresolved: ${missing.join(", ")}`;
}
