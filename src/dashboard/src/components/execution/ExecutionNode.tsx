"use client";

import { useState, type ReactNode } from "react";
import { TimingGutter, type NodeStatus } from "./TimingGutter";
import { LiveTail } from "./LiveTail";

// p0183: one row of the execution tree. Lead column holds depth-indent +
// chevron + status dot + label; the rest of the row is the timing gutter
// and the formatted duration. Tail (one-line latest-event preview) shows
// under the row when the node carries one. Body (optional ReactNode) opens
// when the row is clicked. Children are nested rows rendered inside the
// body — depth comes from the assembler, not from React tree position.

export interface ExecutionNodeProps {
  id: string;
  label: string;
  labelMono?: boolean;
  status: NodeStatus;
  depth: number;
  startSeconds: number;
  durationSeconds: number;
  totalSeconds: number;
  durationLabel: string;
  tail?: { text: string; timestamp: string };
  body?: ReactNode;
  children?: ExecutionNodeProps[];
  /** Keep the body rendered (always-open). Used by the synthetic root node
   *  in the system page so subsystems don't collapse. */
  alwaysOpen?: boolean;
}

export function ExecutionNode(props: ExecutionNodeProps) {
  const [open, setOpen] = useState(false);
  const isOpen = props.alwaysOpen || open;
  const hasChildren = (props.children?.length ?? 0) > 0;
  const expandable = props.body !== undefined || hasChildren;
  const indent = props.depth * 22;
  const leadIndent = 56 + indent;

  return (
    <div
      data-testid={`execution-node-${props.id}`}
      data-open={isOpen ? "true" : "false"}
      className="border-b border-stone-100 last:border-b-0"
    >
      <button
        type="button"
        data-testid={`execution-node-${props.id}-row`}
        onClick={() => expandable && setOpen((o) => !o)}
        className="flex w-full items-center px-3.5 text-left min-h-[42px] hover:bg-stone-50 disabled:cursor-default"
        disabled={!expandable}
      >
        <div className="flex w-[326px] flex-none items-center gap-2.5">
          <span style={{ width: indent }} aria-hidden="true" />
          {expandable ? (
            <span
              className={`w-3 flex-none text-[10px] text-stone-400 transition-transform ${
                isOpen ? "rotate-90" : ""
              }`}
              aria-hidden="true"
            >
              ▶
            </span>
          ) : (
            <span className="w-3 flex-none" aria-hidden="true" />
          )}
          <StatusDot status={props.status} />
          <span
            className={`truncate text-sm font-medium ${
              props.labelMono ? "font-mono text-[13px]" : ""
            } ${props.status === "fail" ? "text-rose-700" : "text-stone-800"}`}
          >
            {props.label}
          </span>
        </div>
        <TimingGutter
          startSeconds={props.startSeconds}
          durationSeconds={props.durationSeconds}
          totalSeconds={props.totalSeconds}
          status={props.status}
        />
        <span className="w-14 flex-none text-right font-mono text-[11px] text-stone-400">
          {props.durationLabel}
        </span>
      </button>
      {props.tail && (
        <LiveTail
          text={props.tail.text}
          timestamp={props.tail.timestamp}
          indentPx={leadIndent}
        />
      )}
      {isOpen && expandable && (
        <div
          data-testid={`execution-node-${props.id}-body`}
          className="border-t border-stone-100 bg-stone-50/40 py-2 pr-3.5"
          style={{ paddingLeft: leadIndent }}
        >
          {props.body}
          {hasChildren && (
            <div className="mt-2 overflow-hidden rounded-md border border-stone-100 bg-white">
              {props.children!.map((c) => (
                <ExecutionNode key={c.id} {...c} />
              ))}
            </div>
          )}
        </div>
      )}
    </div>
  );
}

function StatusDot({ status }: { status: NodeStatus }) {
  const cls = statusDotClass(status);
  return (
    <span
      data-testid={`status-dot-${status}`}
      className={`h-2.5 w-2.5 flex-none rounded-full ${cls}`}
      aria-label={status}
    />
  );
}

function statusDotClass(status: NodeStatus): string {
  switch (status) {
    case "ok":
      return "bg-emerald-500";
    case "fail":
      return "bg-rose-500";
    case "run":
      return "bg-amber-500 animate-pulse";
    case "wait":
      return "bg-stone-300";
  }
}
