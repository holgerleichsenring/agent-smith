"use client";

import type { ReactNode } from "react";
import { useLiveAttempt } from "@/lib/tickets/TicketPageStore";
import type { RunEvent } from "@/types/hub-events";
import type { RunPasses, TicketAttempt, TicketConversationEntry, TicketDetail, TicketSeries } from "@/types/tickets";
import type { AttemptFlowLayout, FlowNode, FlowState } from "./attemptFlowLayout";
import { buildDetail, questionDetail, specDetail, tailDetail, verifyDetail } from "./nodeDetailBody";
import { deriveDetail, gateDetail, handbackDetail, ticketDetail } from "./nodeDetailHead";

// 2026-10-13-9a59q: the mock's .sdet — the chosen node explained from the attempt's own records, light,
// below the panel. A node whose kind has nothing recorded says so; it is never filled from a neighbour.

export interface NodeContext {
  node: FlowNode;
  detail: TicketDetail;
  attempt: TicketAttempt;
  nextAttempt: TicketAttempt | null;
  /** series?runId= of this attempt. */
  series: TicketSeries | null;
  passes: RunPasses | null;
  entries: TicketConversationEntry[];
  /** The live stream — empty unless this attempt is the live one. */
  liveEvents: RunEvent[];
}

export interface NodeExplained {
  title: string;
  left: ReactNode;
  right?: ReactNode;
}

export const EMPTY_RECORD = "Nothing recorded here in this attempt.";

const PILL: Record<FlowState, [string, string]> = {
  done: ["ok", "done here"],
  carried: ["q", "carried"],
  live: ["amber", "working now"],
  bad: ["bad", "stopped here"],
  idle: ["q", "not started"],
  person: ["person", "waits for a person"],
};

interface Props {
  layout: AttemptFlowLayout;
  nodeId: string;
  detail: TicketDetail;
  attempt: TicketAttempt;
  nextAttempt: TicketAttempt | null;
  series: TicketSeries | null;
  passes: RunPasses | null;
  entries: TicketConversationEntry[];
}

export function NodeDetail({ layout, nodeId, ...reads }: Props) {
  const live = useLiveAttempt();
  const node = layout.nodes.find((n) => n.id === nodeId);
  if (!node) return null;
  const liveEvents = live.attempt?.runId === reads.attempt.runId ? live.events : [];
  const context: NodeContext = { node, liveEvents, ...reads };
  const explained = explain(context);
  const [pill, word] = PILL[node.state];
  return (
    <div className="sdet" aria-live="polite" data-testid="node-detail">
      <div className="sd-h">
        <h3>{explained?.title ?? titleOf(node)}</h3>
        <span className={`pill ${pill}`}>{word}</span>
        <span className="sh-sub">A{reads.attempt.number}{node.fullMetric ? ` · ${node.fullMetric}` : ""}</span>
      </div>
      <div className={explained?.right ? "sd-b" : "sd-b one"}>
        <div>{explained ? explained.left : <div className="why"><p>{EMPTY_RECORD}</p></div>}</div>
        {explained?.right && <div>{explained.right}</div>}
      </div>
    </div>
  );
}

function explain(context: NodeContext): NodeExplained | null {
  switch (context.node.kind) {
    case "ticket": return ticketDetail(context);
    case "derive": return deriveDetail(context);
    case "handback": return handbackDetail(context);
    case "gate": return gateDetail(context);
    case "spec": return specDetail(context);
    case "build": return buildDetail(context);
    case "verify": return verifyDetail(context);
    case "question": return questionDetail(context);
    case "tail": return tailDetail(context);
  }
}

const TITLES: Record<string, string> = {
  ticket: "Ticket", derive: "Derive · cut and review", handback: "Hand-back", gate: "Spec gate",
  spec: "Spec", build: "Build", verify: "Verify", question: "Question", tail: "End of attempt",
};

export function titleOf(node: FlowNode): string {
  return TITLES[node.kind];
}

export function Why({ lines }: { lines: ReactNode[] }) {
  return <div className="why">{lines.map((line, i) => <p key={i}>{line}</p>)}</div>;
}

export function Doc({ heading, children }: { heading: string; children: ReactNode }) {
  return <div className="doc"><h5>{heading}</h5>{children}</div>;
}
