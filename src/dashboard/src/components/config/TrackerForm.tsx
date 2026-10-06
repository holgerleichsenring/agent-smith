"use client";

import { useState, type ReactNode } from "react";
import type { ConfigCapabilities, ConfigFinding, StudioTracker } from "@/lib/configApi";
import { cn } from "@/lib/utils";
import { SelectField } from "./formFields";
import { CapabilityFieldInputs, pruneToType, type FieldValue } from "./capabilityFields";
import { DraftCheckPanel } from "./DraftCheckPanel";
import { TrackerOutcomes } from "./TrackerOutcomes";
import { TrackerPollingBlock } from "./TrackerPollingBlock";
import { OUTCOME_GROUP, TRACKER_TABS, trackerTabFor, trackerTabOf, trackerTabTitle, type TrackerTab } from "./trackerTabs";

// 2026-10-06-cea8: the tracker drawer as tabs (the ProjectForm/AgentForm pattern). It was one
// column of some twenty controls; each tab now answers one question — where the board is,
// which tickets are taken, where a finished run moves a ticket, which pipeline runs, what is
// filed. The field list is still the descriptor's; only the placement is read from its group.

export function TrackerForm({
  draft,
  onChange,
  capabilities,
  findings,
  secrets,
  idField,
}: {
  draft: StudioTracker;
  onChange: (next: StudioTracker) => void;
  capabilities: ConfigCapabilities | null;
  findings: ConfigFinding[];
  secrets: string[];
  idField: ReactNode;
}) {
  const [tab, setTab] = useState<TrackerTab>("connection");
  const fields = capabilities?.trackerTypes.find((d) => d.type === draft.type)?.fields ?? [];
  const values = draft as unknown as Record<string, unknown>;
  const onFieldChange = (key: string, value: FieldValue) => onChange({ ...draft, [key]: value });
  const on = (t: TrackerTab) => fields.filter((f) => trackerTabOf(f) === t);
  const visible = TRACKER_TABS.filter((t) => t === "connection" || t === "intake" || on(t).length > 0);
  const marked = new Set(
    findings.filter((f) => f.severity === "blocking" && f.field).map((f) => trackerTabFor(f.field!, fields)),
  );
  const current = visible.includes(tab) ? tab : "connection";
  const inputs = (list = on(current)) => (
    <CapabilityFieldInputs
      fields={list}
      values={values}
      onFieldChange={onFieldChange}
      findings={findings}
      secrets={secrets}
    />
  );

  return (
    <div className="flex flex-col gap-4">
      <div className="dtabs" role="tablist" aria-label="Tracker sections">
        {visible.map((key) => (
          <button
            key={key}
            type="button"
            role="tab"
            aria-selected={current === key}
            className={cn("dtab", current === key && "on")}
            data-marked={marked.has(key) ? "true" : "false"}
            data-testid={`tracker-tab-${key}`}
            onClick={() => setTab(key)}
          >
            {trackerTabTitle(key)}
            {marked.has(key) && <span className="dtab-mark" aria-label="needs attention">▲</span>}
          </button>
        ))}
      </div>

      <div className="flex flex-col gap-4" data-testid={`tracker-panel-${current}`} role="tabpanel">
        {current === "connection" && (
          <>
            {idField}
            <SelectField
              label="type"
              value={draft.type}
              options={capabilities?.trackerTypes.map((d) => d.type) ?? []}
              required
              help={capabilities ? undefined : "capabilities unavailable"}
              testId="form-field-type"
              onChange={(v) => onChange(pruneToType(draft, capabilities?.trackerTypes ?? [], v))}
            />
            {inputs()}
            <DraftCheckPanel kind="trackers" draft={draft} />
          </>
        )}
        {current === "intake" && (
          <>
            {inputs()}
            <TrackerPollingBlock tracker={draft} onChange={onChange} />
          </>
        )}
        {current === "outcomes" && (
          <>
            <TrackerOutcomes
              fields={fields.filter((f) => f.group === OUTCOME_GROUP)}
              values={values}
              onFieldChange={onFieldChange}
              findings={findings}
            />
            {inputs(on("outcomes").filter((f) => f.group !== OUTCOME_GROUP))}
          </>
        )}
        {(current === "routing" || current === "filing") && inputs()}
      </div>
    </div>
  );
}
