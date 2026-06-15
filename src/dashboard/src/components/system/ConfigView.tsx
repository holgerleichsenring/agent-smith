"use client";

import { useEffect, useState } from "react";
import { fetchConfig, type ConfigSnapshot } from "@/lib/configApi";
import { ProjectSelect } from "./ProjectSelect";
import { ProjectDetailPanel } from "./ProjectDetailPanel";
import { SubsystemDetail } from "./SubsystemDetail";
import { SectionLabel } from "@/components/ui/SectionLabel";
import type { SubsystemActivity } from "@/hooks/useSubsystemActivity";

// p0271: the System → Config view. The topology graph + global-defaults grid are
// gone (decoration that rendered sameness); instead a project combobox drives one
// dense, hierarchical detail SHEET for the selected project — agent, repos (with
// full URLs), tracker (with how-it-tracks config), and resolved effective
// settings. The config-file READ-events stream stays below. The snapshot is a
// redacted allow-list (no secret ever reaches the dashboard).

export function ConfigView({ activity }: { activity: SubsystemActivity }) {
  const [config, setConfig] = useState<ConfigSnapshot | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [selected, setSelected] = useState<string>("");

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

  return (
    <div className="flex h-full flex-col overflow-y-auto" data-testid="config-view">
      <div className="content-shell pb-0">
        <SectionLabel>Resolved configuration</SectionLabel>
        <p className="mt-1 dsh-body text-stone-500">
          How agent-smith is wired — pick a project to see everything reachable around it.
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
      ) : config.projects.length === 0 ? (
        <div className="content-shell dsh-body text-stone-500" data-testid="config-view-empty">
          No projects configured.
        </div>
      ) : (
        <>
          <div className="content-shell pb-0 pt-4">
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
        </>
      )}

      <div className="mt-6 border-t border-stone-200 pt-2">
        <SubsystemDetail activity={activity} />
      </div>
    </div>
  );
}
