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
}

export function SandboxBox({ runId, repo, expanded, onToggle }: Props) {
  const feed = useSandboxEvents(runId, repo, expanded);
  const { state: filterState } = useEventFilter();
  const command = feed.command as SandboxCommandEvent | null;

  const stdoutAllowed = isAllowed(filterState, EventType.SandboxOutput);
  const visibleOutputs = stdoutAllowed ? feed.outputs : [];

  return (
    <div className="rounded-md border border-stone-200" data-testid={`sandbox-box-${repo}`}>
      <button
        type="button"
        onClick={onToggle}
        className="flex w-full items-center justify-between px-3 py-2 text-sm hover:bg-stone-50"
        aria-expanded={expanded}
      >
        <span className="flex items-center gap-2">
          <span className="font-medium text-stone-800">{repo}</span>
          {command && (
            <span className="font-mono text-xs text-stone-500">
              {command.command} ({command.argsLength}B args)
            </span>
          )}
        </span>
        <span className="text-xs text-stone-400">{expanded ? "− collapse" : "+ expand"}</span>
      </button>
      {expanded && (
        <div className="border-t border-stone-200 bg-stone-950 p-3 font-mono text-xs text-stone-100"
             data-testid={`sandbox-output-${repo}`}>
          {visibleOutputs.length === 0 ? (
            <p className="text-stone-500">{stdoutAllowed ? "waiting for stdout…" : "stdout filtered off"}</p>
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
