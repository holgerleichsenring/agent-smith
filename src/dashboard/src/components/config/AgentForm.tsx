"use client";

import { useState } from "react";
import type { ConfigCapabilities, StudioAgent } from "@/lib/configApi";
import { cn } from "@/lib/utils";
import type { ConfigCatalog } from "./useConfigCatalog";
import { AgentModelsTab } from "./agent/AgentModelsTab";
import { AgentModelEditor } from "./agent/AgentModelEditor";
import { AgentActivitiesTab } from "./agent/AgentActivitiesTab";
import { AgentProviderTab } from "./agent/AgentProviderTab";
import { AgentTuningTab } from "./agent/AgentTuningTab";
import { strongMismatches } from "./agent/modelCatalog";
import { usePriceList } from "./agent/usePriceList";

// 2026-09-30-62bab: the agent drawer as four tabs (the ProjectForm pattern). An agent
// declares its models ONCE in a named catalog (Models), each activity picks one entry
// (Activities), and the agent's own connection and tuning keep their fields. Adding or
// editing a model is a sub-view of the Models tab inside the same drawer body.

const TABS = ["models", "activities", "provider", "tuning"] as const;
export type AgentTab = (typeof TABS)[number];

/** null = no sub-view; "" = adding; otherwise the entry being edited. */
type Editing = string | null;

export function AgentForm({
  draft,
  onChange,
  catalog,
  capabilities,
}: {
  draft: StudioAgent;
  onChange: (next: StudioAgent) => void;
  catalog: ConfigCatalog;
  capabilities: ConfigCapabilities | null;
}) {
  const [tab, setTab] = useState<AgentTab>("models");
  const [editing, setEditing] = useState<Editing>(null);
  const { list: prices, failed: pricesFailed } = usePriceList();
  const entryCount = Object.keys(draft.catalog ?? {}).length;
  const marked = new Set<AgentTab>();
  if (strongMismatches(draft, capabilities).length > 0 || !draft.models.primary) marked.add("activities");

  if (editing !== null) {
    return (
      <AgentModelEditor
        key={editing}
        draft={draft}
        onChange={onChange}
        editing={editing === "" ? null : editing}
        prices={prices}
        capabilities={capabilities}
        onDone={() => setEditing(null)}
      />
    );
  }

  return (
    <div className="flex flex-col gap-4">
      <div className="dtabs" role="tablist" aria-label="Agent sections">
        {TABS.map((key) => (
          <button
            key={key}
            type="button"
            role="tab"
            aria-selected={tab === key}
            className={cn("dtab", tab === key && "on")}
            data-marked={marked.has(key) ? "true" : "false"}
            data-testid={`agent-tab-${key}`}
            onClick={() => setTab(key)}
          >
            {key === "models" ? `Models · ${entryCount}` : key[0].toUpperCase() + key.slice(1)}
            {marked.has(key) && <span className="dtab-mark" aria-label="needs attention">▲</span>}
          </button>
        ))}
      </div>

      <div className="flex flex-col gap-4" data-testid={`agent-panel-${tab}`} role="tabpanel">
        {tab === "models" && (
          <AgentModelsTab
            draft={draft}
            onChange={onChange}
            prices={prices}
            pricesFailed={pricesFailed}
            onAdd={() => setEditing("")}
            onEdit={(name) => setEditing(name)}
          />
        )}
        {tab === "activities" && (
          <AgentActivitiesTab draft={draft} onChange={onChange} capabilities={capabilities} prices={prices} />
        )}
        {tab === "provider" && (
          <AgentProviderTab draft={draft} onChange={onChange} catalog={catalog} capabilities={capabilities} />
        )}
        {tab === "tuning" && <AgentTuningTab draft={draft} onChange={onChange} />}
      </div>
    </div>
  );
}
