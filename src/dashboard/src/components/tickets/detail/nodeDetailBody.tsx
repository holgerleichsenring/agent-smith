import { EventType, type RunEvent, type ToolCallEvent } from "@/types/hub-events";
import type { RunPass, TicketSeriesSpec } from "@/types/tickets";
import { money } from "../ticketFormat";
import { duration } from "./attemptFlowLayout";
import { clock, passEndWords, statusWords } from "./flowWords";
import { questionJoin } from "./questionJoin";
import { Doc, Why, type NodeContext, type NodeExplained } from "./NodeDetail";
import { OpenSpecButton } from "./OpenSpecButton";

// 2026-10-13-9a59q: what the body nodes say — spec, build, verify, question and tail. A pass speaks
// only for itself: an earlier pass's verify lists its own criterion verdicts, not the spec's final ones.

const LIVE_TOOL_CALLS = 5;

function specOf({ node, series }: NodeContext): TicketSeriesSpec | null {
  return series?.specs.find((s) => s.specId === node.specId) ?? null;
}

function passTitle(kind: string, spec: TicketSeriesSpec | null, pass: RunPass | undefined, count: number | undefined): string {
  const passPart = pass && (count ?? 1) > 1 ? ` · pass ${pass.passInSpec}` : "";
  return `${kind}${spec ? ` · spec ${spec.ordinal}` : ""}${passPart}`;
}

export function specDetail(context: NodeContext): NodeExplained | null {
  const spec = specOf(context);
  if (!spec) return null;
  const revision = context.attempt.revision;
  const carried = spec.state === "carried";
  const origin = revision ? `revision ${revision.number}${revision.cause ? ` · ${revision.cause}` : ""}` : null;
  const executed = spec.executedByAttempt != null ? `attempt ${spec.executedByAttempt}` : "an earlier attempt";
  return {
    title: `Spec ${spec.ordinal}`,
    left: (
      <Doc heading={`spec ${spec.ordinal}${spec.repos.length ? ` · ${spec.repos.join(", ")}` : ""}`}>
        <p data-testid="spec-goal">{spec.goal}</p>
        <p className="dim-line">{spec.steps.length} steps · {spec.done.length} done criteria{origin ? ` · ${origin}` : ""}</p>
      </Doc>
    ),
    right: (
      <>
        <Why lines={[carried
          ? `Executed in ${executed}. Its commits are on the branch; nothing ran here.`
          : "Steps and done criteria are known from the moment the spec is cut, and fill in as they are worked."]} />
        <OpenSpecButton specId={spec.specId} />
      </>
    ),
  };
}

export function buildDetail(context: NodeContext): NodeExplained | null {
  const spec = specOf(context);
  const pass = context.node.pass;
  const live = context.node.state === "live";
  const tail = live ? lastToolCalls(context.liveEvents) : [];
  const open = (spec?.worked ?? []).filter((w) => w.state !== "done").map((w) => w.stepId);
  if (!pass && !live && !spec) return null;
  const title = passTitle("Build", spec, pass, context.node.passCount);
  const moved = pass?.stepsMoved ?? [];
  const rows = moved.length > 0
    ? moved.map((t, i) => ({ key: `${t.entryId}-${i}`, cls: t.to === "done" ? "done" : "idle", n: t.entryId, text: t.activity, meta: `${t.from ?? "—"} → ${t.to ?? "—"}` }))
    : !pass && spec ? spec.steps.map((s) => ({ key: s.id, cls: "idle", n: s.id, text: s.action, meta: "" })) : [];
  return {
    title,
    left: (
      <>
        {rows.length > 0 ? <Jobs rows={rows} /> : <Why lines={[pass ? "This pass moved no step." : "No pass is recorded yet."]} />}
        {open.length > 0 && <p className="dim-line" data-testid="open-steps">Open steps: {open.join(", ")}</p>}
      </>
    ),
    right: tail.length > 0 ? <LiveTail calls={tail} /> : pass ? <Why lines={[`${money(pass.costUsd)} over ${pass.calls} model calls.`]} /> : undefined,
  };
}

export function verifyDetail(context: NodeContext): NodeExplained | null {
  const spec = specOf(context);
  const pass = context.node.pass;
  const title = passTitle("Verify", spec, pass, context.node.passCount);
  if (!pass) {
    if (!spec || context.node.state === "live") return null;
    return { title, left: <Criteria rows={spec.done.map((c) => ({ mark: "q", text: c.text, note: "not started" }))} /> };
  }
  const tally = pass.criteriaMet != null && pass.criteriaTotal != null ? `${pass.criteriaMet} of ${pass.criteriaTotal} met · ` : "";
  const reason = pass.reengageReason ? ` (${passEndWords(pass.reengageReason)})` : "";
  return {
    title,
    left: pass.criteria && pass.criteria.length > 0
      ? <Criteria rows={pass.criteria.map((c) => ({ mark: c.status === "met" ? "ok" : c.status === "unmet" ? "bad" : "q", text: c.criterion, note: c.evidence }))} />
      : <Why lines={["This pass recorded no criterion verdicts."]} />,
    right: <Why lines={[<>{tally}The pass ended: <b>{passEndWords(pass.endedBecause)}</b>{reason}.</>]} />,
  };
}

export function questionDetail(context: NodeContext): NodeExplained | null {
  const pass = context.node.pass;
  const join = questionJoin(context.passes, context.entries);
  const pair = pass ? join.pairs.get(pass.ordinal) : undefined;
  if (!pair) return null;
  const { question, answer, parkedMs } = pair;
  const who = answer ? answer.author ?? answer.role : null;
  return {
    title: passTitle("Question", specOf(context), pass, context.node.passCount),
    left: (
      <Why lines={[
        <><b>Question:</b> {question.text}</>,
        answer ? <><b>Answer</b> by {who}{answer.at ? ` at ${clock(answer.at)}` : ""}: {answer.text}</> : "Not answered yet.",
        parkedMs != null ? `Parked ${duration(parkedMs)}; the same attempt resumed.` : null,
      ].filter(Boolean)} />
    ),
  };
}

export function tailDetail({ attempt, detail, passes }: NodeContext): NodeExplained | null {
  const records = detail.summary.pullRequests ?? [];
  const rows = attempt.pullRequests.map((pr) => {
    const record = pr.url ? records.find((r) => r.url === pr.url) : undefined;
    const state = record ? [record.state, record.decision !== "none" ? record.decision.replace(/-/g, " ") : ""].filter(Boolean).join(" · ") : pr.status;
    return { key: `${pr.repo}-${pr.url ?? pr.status}`, cls: pr.status === "failed" ? "bad" : "done", n: record ? `#${record.number}` : "", text: pr.repo, meta: state };
  });
  const last = [...(passes?.specs.flatMap((s) => s.passes) ?? [])].sort((l, r) => l.ordinal - r.ordinal).pop();
  const lines = [
    <>Attempt {attempt.number} <b>{attempt.handedBack ? "was handed back" : statusWords(attempt.status)}</b>{attempt.endedOnInfrastructure ? ", ended on infrastructure" : ""}.</>,
    last ? `Its last pass ended: ${passEndWords(last.endedBecause)}.` : null,
  ].filter(Boolean);
  return {
    title: attempt.handedBack || !["success", "shortfall"].includes(attempt.status) ? "End of attempt" : "Ship",
    left: rows.length > 0 ? <Jobs rows={rows} /> : <Why lines={lines} />,
    right: rows.length > 0 ? <Why lines={lines} /> : undefined,
  };
}

function lastToolCalls(events: RunEvent[]): ToolCallEvent[] {
  return events.filter((e): e is ToolCallEvent => e.type === EventType.ToolCall).slice(-LIVE_TOOL_CALLS);
}

function LiveTail({ calls }: { calls: ToolCallEvent[] }) {
  return (
    <div className="term" data-testid="live-tail">
      {calls.map((call, i) => (
        <div key={`${call.timestamp}-${i}`}>
          <span className="t">{clock(call.timestamp)?.split(" ")[1]}</span> <span className="g">{call.tool}</span> {call.summary ?? ""}
        </div>
      ))}
    </div>
  );
}

interface JobRow { key: string; cls: string; n: string; text: string; meta: string }

function Jobs({ rows }: { rows: JobRow[] }) {
  return (
    <div className="jobs">
      {rows.map((r) => (
        <div key={r.key} className={`job ${r.cls}`}>
          <span className="ji" /><span className="jn">{r.n}</span><span className="jt">{r.text}</span><span className="jm">{r.meta}</span>
        </div>
      ))}
    </div>
  );
}

function Criteria({ rows }: { rows: Array<{ mark: string; text: string; note: string }> }) {
  return (
    <div className="crit" data-testid="criteria">
      {rows.map((r, i) => (
        <div key={i} className="ci">
          <span className={`m ${r.mark}`}>{r.mark === "ok" ? "✓" : r.mark === "bad" ? "✗" : "·"}</span>
          <span>{r.text}</span><span className="cw">{r.note}</span>
        </div>
      ))}
    </div>
  );
}
