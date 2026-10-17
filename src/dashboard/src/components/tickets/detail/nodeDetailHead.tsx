import { TrackerText } from "@/components/ui/TrackerText";
import type { TicketConversationEntry } from "@/types/tickets";
import { causeWords, money } from "../ticketFormat";
import { clock, cutReviews, handbackCaseWords } from "./flowWords";
import { Doc, Why, type NodeContext, type NodeExplained } from "./NodeDetail";

// 2026-10-13-9a59q: what the head nodes say — ticket, derive, hand-back and gate — each from this
// attempt's records only. Null means nothing is recorded for the node.

interface EditDiff { before?: string | null; after?: string | null }

export function ticketDetail({ attempt, entries }: NodeContext): NodeExplained | null {
  const edit = entries.find((e) => e.source === "ticket" && e.kind === "edited");
  const cause = attempt.cause;
  if (!cause && !edit) return null;
  const at = clock(cause?.at);
  const lines: React.ReactNode[] = cause ? [<>Started by <b>{causeWords(cause)}</b>{at ? ` at ${at}` : ""}.</>] : [];
  if (cause?.source?.url) lines.push(<a href={cause.source.url} target="_blank" rel="noreferrer">{cause.source.kind} ↗</a>);
  if (edit) lines.push("The ticket text differs from what the previous attempt read. Trackers do not say who edited.");
  return { title: "Ticket", left: <Why lines={lines} />, right: edit ? <EditDoc entry={edit} /> : undefined };
}

function EditDoc({ entry }: { entry: TicketConversationEntry }) {
  const fields = Object.entries((entry.detail ?? {}) as Record<string, EditDiff>);
  return (
    <Doc heading="what changed">
      {fields.map(([field, diff]) => (
        <p key={field} data-testid={`edit-${field}`}>
          <b>{field}</b><br />− {diff.before ?? ""}<br />+ {diff.after ?? ""}
        </p>
      ))}
    </Doc>
  );
}

export function deriveDetail({ passes, entries }: NodeContext): NodeExplained | null {
  const reviews = cutReviews(entries);
  const cost = passes && passes.derive.calls > 0 ? passes.derive : null;
  if (!cost && reviews.length === 0) return null;
  const lines = cost ? [<>Derivation cost <b>{money(cost.costUsd)}</b> over {cost.calls} model calls.</>] : [];
  const refused = reviews.filter((r) => r.detail.deliverable === "refused");
  return {
    title: "Derive · cut and review",
    left: (
      <>
        {lines.length > 0 && <Why lines={lines} />}
        {reviews.length > 0 && (
          <div className="jobs" data-testid="cut-reviews">
            {reviews.map(({ entry, detail }, i) => (
              <div key={entry.id} className={`job ${detail.deliverable === "refused" ? "bad" : "done"}`}>
                <span className="ji" /><span className="jn">cut {detail.attempt ?? i + 1}</span>
                <span className="jt">{(detail.findings ?? []).length} findings</span>
                <span className="jm">{detail.deliverable ?? ""}</span>
              </div>
            ))}
          </div>
        )}
      </>
    ),
    right: refused.length > 0 ? (
      <Doc heading="why the reviewer refused">
        <ul>
          {refused.flatMap(({ entry, detail }) => (detail.findings ?? []).map((f, i) => (
            <li key={`${entry.id}-${i}`}><b>{f.specId}</b>{f.criterion ? ` · ${f.criterion}` : ""}: {f.problem}{f.why ? ` — ${f.why}` : ""}</li>
          )))}
        </ul>
      </Doc>
    ) : undefined,
  };
}

export function handbackDetail({ series, entries, nextAttempt }: NodeContext): NodeExplained | null {
  const entry = entries.find((e) => e.kind === "hand-back");
  const handbackCase = series?.handback?.case ?? null;
  if (!entry && !handbackCase) return null;
  const quote = (entry?.detail as { quote?: string | null } | undefined)?.quote ?? null;
  const answer = nextAttempt?.cause;
  const lines = [
    handbackCase ? <>Case: <b>{handbackCaseWords(handbackCase)}</b>.</> : null,
    "A hand-back has no paused run to resume: the reply starts the next attempt.",
  ].filter(Boolean);
  return {
    title: "Hand-back",
    left: <Why lines={lines} />,
    right: entry ? (
      <Doc heading="posted on the ticket">
        {entry.text && <div data-testid="handback-text"><TrackerText text={entry.text} format={entry.format} /></div>}
        {quote && <blockquote className="quote"><TrackerText text={quote} format="text" /></blockquote>}
        {answer?.actor && <p className="dim-line">Answered by {answer.actor}{answer.at ? ` at ${clock(answer.at)}` : ""}.</p>}
      </Doc>
    ) : undefined,
  };
}

export function gateDetail({ series, attempt }: NodeContext): NodeExplained | null {
  const gate = series?.runId === attempt.runId ? series.gate : null;
  if (!gate) return null;
  return {
    title: "Spec gate",
    left: <Why lines={[<>The gate <b>{gate.status}</b> with {gate.specCount} specs in the cut.</>]} />,
  };
}
