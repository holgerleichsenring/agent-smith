"use client";

import { useSandboxEvents } from "@/hooks/useSandboxEvents";
import { useEventFilter } from "@/lib/EventFilterContext";
import { isAllowed } from "@/lib/eventFilterQuery";
import { EventType, type SandboxCommandEvent } from "@/types/hub-events";

interface Props {
  runId: string;
  repo: string;
  expanded: boolean;
  onToggle: () => void;
  /**
   * p0175-fix: when SandboxBox renders inside TopologyDetail the operator
   * has explicitly selected the sandbox to see its stdout — the
   * FilterRail's L3 default-off (inherited from p0169g's "L3 events
   * default to off matching the hub's gated fanout") shouldn't gate that
   * content. Pass true to bypass the L3 filter.
   */
  ignoreL3Filter?: boolean;
  /**
   * p0173f: when a sub-agent currently operates this sandbox, surface
   * that identity inline. Resolved upstream by SandboxList from the
   * latest SubAgentToolCallEvent or SubAgentFileWrittenEvent on this
   * sandbox; null when no sub-agent has touched it (the master alone).
   */
  operatingSubAgentName?: string | null;
  /** p0203: when the parent knows the step finished (success or fail) it
   *  passes the run duration so the collapsed placeholder can read
   *  "step ran for X seconds (stdout hidden, click to expand)" instead
   *  of the generic "waiting for stdout…". Null = still running. */
  finishedDurationMs?: number | null;
}

export function SandboxBox({
  runId, repo, expanded, onToggle, ignoreL3Filter = false,
  operatingSubAgentName = null, finishedDurationMs = null,
}: Props) {
  const feed = useSandboxEvents(runId, repo, expanded);
  const { state: filterState } = useEventFilter();
  const command = feed.command as SandboxCommandEvent | null;

  const stdoutAllowed = ignoreL3Filter || isAllowed(filterState, EventType.SandboxOutput);
  const visibleOutputs = stdoutAllowed ? feed.outputs : [];

  return (
    <div className="rounded-md border border-stone-200" data-testid={`sandbox-box-${repo}`}>
      <SandboxHeader
        repo={repo}
        operatingSubAgentName={operatingSubAgentName}
        command={command}
        expanded={expanded}
        finishedDurationMs={finishedDurationMs}
        onToggle={onToggle}
      />
      {expanded && (
        <div className="border-t border-stone-200 bg-stone-950 p-3 font-mono text-xs text-stone-100"
             data-testid={`sandbox-output-${repo}`}>
          {visibleOutputs.length === 0 ? (
            <p className="text-stone-500">
              {placeholderText(stdoutAllowed, finishedDurationMs)}
            </p>
          ) : (
            visibleOutputs.map((o, idx) => (
              <div key={`${o.batchSeq}-${idx}`} className={o.stream === "stderr" ? "text-rose-300" : ""}>
                {o.line}
              </div>
            ))
          )}
        </div>
      )}
    </div>
  );
}

function SandboxHeader({
  repo, operatingSubAgentName, command, expanded, finishedDurationMs, onToggle,
}: {
  repo: string;
  operatingSubAgentName: string | null;
  command: SandboxCommandEvent | null;
  expanded: boolean;
  finishedDurationMs: number | null;
  onToggle: () => void;
}) {
  return (
    <button
      type="button"
      onClick={onToggle}
      className="flex w-full items-center justify-between px-3 py-2 text-sm hover:bg-stone-50"
      aria-expanded={expanded}
    >
      <span className="flex items-center gap-2">
        <span className="font-medium text-stone-800">{repo}</span>
        {operatingSubAgentName && (
          <span
            data-testid={`sandbox-operator-${repo}`}
            className="rounded bg-emerald-100 px-1.5 py-0.5 font-mono dsh-label text-emerald-900"
          >
            {operatingSubAgentName}
          </span>
        )}
        {command && (
          <span className="font-mono text-xs text-stone-500">
            {command.command}
            {command.summary ? ` ${command.summary}` : ` (${command.argsLength}B args)`}
          </span>
        )}
      </span>
      <span data-testid={`sandbox-toggle-${repo}`} className="text-xs text-stone-400">
        {expanded
          ? "− collapse"
          : finishedDurationMs !== null
            ? `+ expand (${formatStepDuration(finishedDurationMs)})`
            : "+ expand"}
      </span>
    </button>
  );
}

function placeholderText(stdoutAllowed: boolean, finishedDurationMs: number | null): string {
  if (!stdoutAllowed) return "stdout filtered off";
  if (finishedDurationMs === null) return "waiting for stdout…";
  return `step ran for ${formatStepDuration(finishedDurationMs)} (stdout hidden, click to expand)`;
}

function formatStepDuration(ms: number): string {
  if (ms < 1000) return `${ms}ms`;
  const s = ms / 1000;
  if (s < 60) return `${s.toFixed(1)}s`;
  const m = Math.floor(s / 60);
  const rem = Math.round(s - m * 60);
  return rem === 0 ? `${m}m` : `${m}m${rem}s`;
}
