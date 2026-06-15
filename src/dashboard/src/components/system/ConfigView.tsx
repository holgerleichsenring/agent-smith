"use client";

import { useEffect, useState } from "react";
import { fetchConfig, type ConfigSnapshot } from "@/lib/configApi";
import { ConfigGraph } from "./ConfigGraph";
import { ConfigDetail } from "./ConfigDetail";
import { ProjectDetailPanel } from "./ProjectDetailPanel";
import { SubsystemDetail } from "./SubsystemDetail";
import { SectionLabel } from "@/components/ui/SectionLabel";
import type { SubsystemActivity } from "@/hooks/useSubsystemActivity";

// p0266: the System → Config view. The resolved-config graph + globals/detail
// cards on top (the config-time "how the system is wired" view), with the
// existing config-file READ-events stream kept below. The snapshot is fetched
// once on mount from /api/config (a redacted allow-list — secrets never reach
// the dashboard); the stream stays live via the shared subsystem activity.

export function ConfigView({ activity }: { activity: SubsystemActivity }) {
  const [config, setConfig] = useState<ConfigSnapshot | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [selected, setSelected] = useState<string | null>(null);

  useEffect(() => {
    const controller = new AbortController();
    fetchConfig(controller.signal)
      .then((snapshot) => {
        setConfig(snapshot);
        // Default-select the first project so the explainer panel is never empty.
        setSelected(snapshot.projects[0]?.name ?? null);
      })
      .catch((e: Error) => {
        if (e.name !== "AbortError") setError(e.message);
      });
    return () => controller.abort();
  }, []);

  const selectedProject = config?.projects.find((p) => p.name === selected) ?? null;

  return (
    <div className="flex h-full flex-col overflow-y-auto" data-testid="config-view">
      <div className="content-shell pb-0">
        <SectionLabel>Resolved configuration</SectionLabel>
        <p className="mt-1 dsh-body text-stone-500">
          How agent-smith is wired — each project and the resources reachable around it.
          Secrets are never sent to the dashboard.
        </p>
      </div>

      {error ? (
        <div className="content-shell dsh-body text-rose-700" data-testid="config-view-error">
          Failed to load config: {error}
        </div>
      ) : !config ? (
        <div className="content-shell dsh-body text-stone-400" data-testid="config-view-loading">
          Loading config…
        </div>
      ) : (
        <>
          <div className="content-shell pt-4">
            <ConfigGraph
              projects={config.projects}
              edges={config.edges}
              selected={selected}
              onSelectProject={setSelected}
            />
          </div>
          {selectedProject && <ProjectDetailPanel project={selectedProject} />}
          <ConfigDetail config={config} />
        </>
      )}

      <div className="mt-6 border-t border-stone-200 pt-2">
        <SubsystemDetail activity={activity} />
      </div>
    </div>
  );
}
