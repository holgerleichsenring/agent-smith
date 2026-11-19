"use client";

import { useEffect, useMemo, useState } from "react";
import type { RunEvent } from "@/types/hub-events";
import { minutesAndSeconds } from "@/components/dialog/DialogElapsed";
import { rowSummary, subAgentRows, type SubAgentRow } from "./subAgentRows";

// 2026-11-19-687eb: an open step's sub-agents, directly under its header — one line each, opened to what its master
// asked of it and the tool calls it made. They were two lines at the bottom of a newest-first list before, and their
// tool calls were nowhere.

interface Props {
  events: RunEvent[];
  live: boolean;
  hasOlder: boolean;
  loading: boolean;
  loadOlder: () => void;
}

export function StepSubAgents({ events, live, hasOlder, loading, loadOlder }: Props) {
  const rows = useMemo(() => subAgentRows(events, { runEnded: !live, hasOlder }), [events, live, hasOlder]);
  const [open, setOpen] = useState<Set<string>>(() => new Set());
  if (rows.length === 0) return null;
  const toggle = (id: string) => setOpen((held) => {
    const next = new Set(held);
    if (next.has(id)) next.delete(id);
    else next.add(id);
    return next;
  });
  return (
    <div className="rl-subs" data-testid="step-sub-agents">
      {rows.map((row) => (
        <div key={row.id} className="rl-sub" data-testid="step-sub-agent" data-running={row.running}>
          <button type="button" className="rl-sub-head" aria-expanded={open.has(row.id)} onClick={() => toggle(row.id)}
            title={row.brief} data-testid="step-sub-agent-toggle">
            <span className="rl-sub-mark" aria-hidden="true">{open.has(row.id) ? "▾" : "▸"}</span>
            <span className="rl-sub-name">{row.name}</span>
            <span className={row.failed ? "rl-bad" : row.running ? "rl-sub-run" : "rl-dim"} data-testid="step-sub-agent-status">
              {rowSummary(row)}
              {row.thinkingSince && <> · thinking · <Since iso={row.thinkingSince} /></>}
            </span>
          </button>
          {open.has(row.id) && <SubAgentBody row={row} hasOlder={hasOlder} loading={loading} loadOlder={loadOlder} />}
        </div>
      ))}
    </div>
  );
}

function SubAgentBody({ row, hasOlder, loading, loadOlder }: { row: SubAgentRow; hasOlder: boolean; loading: boolean; loadOlder: () => void }) {
  return (
    <div className="rl-sub-body" data-testid="step-sub-agent-body">
      <div className="rl-sub-brief" data-testid="step-sub-agent-brief">{row.brief}</div>
      {row.calls.map((call) => (
        <div key={call.key} className="rl-detail-line" data-testid="step-sub-agent-call">
          <span className="rl-at">{call.at}</span>
          <span className="rl-kind">{call.tool}</span>
          <span className="rl-text">{call.target}</span>
          <span className="rl-meta-col" />
        </div>
      ))}
      {row.calls.length === 0 && !row.partial && <div className="rl-detail-line rl-dim">no tool calls recorded</div>}
      {row.partial && hasOlder && (
        <button type="button" className="rl-filter" disabled={loading} onClick={loadOlder} data-testid="step-sub-agent-older">
          {loading ? "loading…" : "load older calls"}
        </button>
      )}
    </div>
  );
}

/** Seconds since a moment, ticking — "8s", then "1m 4s". */
function Since({ iso }: { iso: string }) {
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    const ticker = setInterval(() => setNow(Date.now()), 1000);
    return () => clearInterval(ticker);
  }, []);
  const seconds = Math.max(0, Math.floor((now - Date.parse(iso)) / 1000));
  return <>{seconds < 60 ? `${seconds}s` : minutesAndSeconds(seconds)}</>;
}
