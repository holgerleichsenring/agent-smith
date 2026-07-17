"use client";

import type { StudioProject } from "@/lib/configApi";
import { WiringChip } from "./primitives";
import { projectIntegrity } from "./integrity";
import type { ConfigCatalog } from "./useConfigCatalog";
import { cn } from "@/lib/utils";

// p0345: the live wiring preview for the project form — agent → project ← tracker
// with repos below — plus an integrity bar that turns green only when every
// reference resolves against the catalog. Recomputes on each keystroke/pick.
// p0343c (pixel identity): emits the config-studio.html .preview-wire DOM
// verbatim — .pw-h heading, .pw-graph with .pw-node nodes (the green .proj
// center, dashed .empty placeholders), .pw-repos chips, and the .integrity bar
// (.warn while unresolved).

export function ProjectWiring({
  project,
  catalog,
}: {
  project: StudioProject;
  catalog: ConfigCatalog;
}) {
  const integrity = projectIntegrity(catalog, project);
  return (
    <div className="field" data-testid="project-wiring">
      <div className="preview-wire">
        <div className="pw-h">Wiring preview</div>
        <div className="pw-graph">
          {project.agent ? (
            <WiringChip label="agent" kind="agent" value={project.agent} resolved={integrity.agentOk} testId="wiring-agent" />
          ) : (
            <span className="pw-node empty" data-testid="wiring-agent" data-resolved="false">
              no agent
            </span>
          )}
          <span className="warr">→</span>
          <span className="pw-node proj">{project.id || "new project"}</span>
          <span className="warr">←</span>
          {project.tracker ? (
            <WiringChip label="tracker" kind="tracker" value={project.tracker} resolved={integrity.trackerOk} testId="wiring-tracker" />
          ) : (
            <span className="pw-node empty" data-testid="wiring-tracker" data-resolved="false">
              no tracker
            </span>
          )}
        </div>
        <div className="pw-repos">
          {integrity.repoResults.length === 0 && (
            <span className="pw-node empty">no repositories</span>
          )}
          {/* p0345b: conn-scoped refs ("conn/Name") resolve via the connections
              catalog and are labeled as such; plain refs stay "repo". */}
          {integrity.repoResults.map((r) => (
            <WiringChip
              key={r.id}
              label={r.id.includes("/") ? "conn repo" : "repo"}
              kind="repo"
              value={r.id}
              resolved={r.ok}
              testId={`wiring-repo-${r.id}`}
            />
          ))}
        </div>
      </div>

      <div
        data-testid="project-integrity"
        data-ok={integrity.ok ? "true" : "false"}
        className={cn("integrity", !integrity.ok && "warn")}
      >
        <span className="ii">{integrity.ok ? "✓" : "▲"}</span>
        <div>
          {integrity.ok
            ? "Every reference resolves to a catalog entry — this project will pass config validation."
            : integrityHint(integrity)}
        </div>
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
