"use client";

import { useEffect, useState } from "react";
import { fetchConfig, type ConfigSnapshot } from "@/lib/configApi";
import { ProjectSelect } from "./ProjectSelect";
import { ProjectDetailPanel } from "./ProjectDetailPanel";
import { SubsystemDetail } from "./SubsystemDetail";
import type { SubsystemActivity } from "@/hooks/useSubsystemActivity";

// p0271: the System → Config view. The topology graph + global-defaults grid are
// gone (decoration that rendered sameness); instead a project combobox drives one
// dense, hierarchical detail SHEET for the selected project — agent, repos (with
// full URLs), tracker (with how-it-tracks config), and resolved effective
// settings. The config-file READ-events stream stays below. The snapshot is a
// redacted allow-list (no secret ever reaches the dashboard).
// p0343d: parity re-dress — the page head moved to SystemView's .m-head; here a
// .section-head rule opens the resolved-config sheet, the read-events stream
// follows as its own section. States use the parity .stateline vocabulary.

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
    <div data-testid="config-view">
      <section>
        <div className="section-head">
          <h2>Resolved configuration</h2>
          {config && config.projects.length > 0 && (
            <span className="cnt">{config.projects.length}</span>
          )}
          <span className="sh-sub">pick a project — everything reachable around it</span>
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
          <>
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
          </>
        )}
      </section>

      <section>
        <SubsystemDetail activity={activity} />
      </section>
    </div>
  );
}
