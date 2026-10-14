"use client";

import { useTicketSeries } from "@/lib/tickets/TicketPageStore";
import type { TicketAttempt, TicketDetail, TicketSeriesSpec } from "@/types/tickets";
import { channelWords, money } from "../ticketFormat";
import { clock, elapsed } from "./flowWords";
import { isRunRecord } from "./specWords";
import { useTicketTabGo } from "./useTicketTabGo";

// 2026-10-13-9a59q: the mock's .sum cells and .banner for one attempt, from its own records — the
// cause, the revision and its carried ids, the ticket's pull-request records, and the attempt's cut
// (series?runId=) for its repositories. A value no read delivers is left out, never estimated.

export function SummaryCells({ detail, attempt }: { detail: TicketDetail; attempt: TicketAttempt }) {
  const cut = useTicketSeries(attempt.runId).data;
  const own = cut?.runId === attempt.runId ? cut : null;
  const specs = own?.specs ?? [];
  return (
    <div className="sum" data-testid="ticket-summary-cells">
      <Cell k={`Attempt ${attempt.number} started by`}>{causeLines(attempt)}</Cell>
      <Cell k="Series">{seriesLines(attempt, specs, isRunRecord(own))}</Cell>
      <Cell k="Pull requests">{prLines(detail)}</Cell>
      <Cell k="This attempt">{attemptLines(attempt, specs)}</Cell>
    </div>
  );
}

function Cell({ k, children }: { k: string; children: React.ReactNode }) {
  return (
    <div className="sg">
      <span className="k">{k}</span>
      <div className="v">{children}</div>
    </div>
  );
}

function causeLines(attempt: TicketAttempt) {
  const cause = attempt.cause;
  if (!cause) return <span className="dim" data-testid="cell-cause-none">not recorded</span>;
  const source = cause.source ? [cause.source.kind, cause.source.detail].filter(Boolean).join(" ") : null;
  const dim = [cause.actor, source, clock(cause.at)].filter(Boolean).join(" · ");
  return (
    <>
      <span>{channelWords(cause.channel)}</span>
      {dim && <span className="dim">{dim}</span>}
    </>
  );
}

function seriesLines(attempt: TicketAttempt, specs: TicketSeriesSpec[], runRecord: boolean) {
  if (specs.length === 0 && !attempt.revision) return <span className="dim">no cut recorded</span>;
  if (runRecord) return <span data-testid="cell-series-run-record">{specs.length} specs · run record</span>;
  const revision = attempt.revision ? ` · revision ${attempt.revision.number}` : "";
  const carried = carriedOf(attempt, specs).length;
  return (
    <>
      <span>{specs.length} specs{revision}</span>
      {attempt.revision && <span className="dim">{carried} carried · {specs.length - carried} cut new</span>}
    </>
  );
}

function prLines(detail: TicketDetail) {
  const prs = detail.summary.pullRequests ?? [];
  if (prs.length === 0) return <span className="dim">none recorded</span>;
  return prs.map((pr) => (
    <span key={pr.url} className="mono">
      #{pr.number} {pr.repo} · {pr.state}{pr.decision && pr.decision !== "none" ? ` · ${pr.decision.replace(/-/g, " ")}` : ""}
    </span>
  ));
}

function attemptLines(attempt: TicketAttempt, specs: TicketSeriesSpec[]) {
  const repos = [...new Set(specs.flatMap((s) => s.repos))];
  const dim = [repos.join(", "), `run ${attempt.runId}`].filter(Boolean).join(" · ");
  return (
    <>
      <span className="mono">{elapsed(attempt.startedAt, attempt.finishedAt)} · {money(attempt.cost)}</span>
      <span className="dim" data-testid="cell-this-attempt">{dim}</span>
    </>
  );
}

function carriedOf(attempt: TicketAttempt, specs: TicketSeriesSpec[]): TicketSeriesSpec[] {
  const ids = new Set(attempt.revision?.carriedIds ?? []);
  return specs.filter((s) => s.state === "carried" || ids.has(s.specId));
}

/** The banner: which specs this attempt carried at $0 and which it cut, from the revision's cause. */
export function CarriedBanner({ attempt }: { attempt: TicketAttempt }) {
  const cut = useTicketSeries(attempt.runId).data;
  const specs = cut?.runId === attempt.runId ? [...cut.specs].sort((l, r) => l.ordinal - r.ordinal) : [];
  const carried = carriedOf(attempt, specs);
  if (!attempt.revision || carried.length === 0) return null;
  const fresh = specs.filter((s) => !carried.includes(s));
  const cause = attempt.revision.cause ? `, ${attempt.revision.cause}` : "";
  const cutWords = fresh.length > 0 ? `This attempt cut ${specList(fresh)}${cause}.` : `This attempt cut no new spec${cause}.`;
  return (
    <div className="banner" data-testid="ticket-carried-banner">
      <span className="bi">i</span>
      <div>
        <div className="bt">{capital(specList(carried))} ran in earlier attempts and {carried.length === 1 ? "is" : "are"} not redone. {cutWords}</div>
        <div className="bs">Executed specs are append-only: the branch keeps them, the series keeps their ids. <b>$0</b> spent on {specList(carried)} this time.</div>
      </div>
      <OpenSpecsButton />
    </div>
  );
}

function specList(specs: TicketSeriesSpec[]): string {
  const ordinals = specs.map((s) => s.ordinal);
  if (ordinals.length === 1) return `spec ${ordinals[0]}`;
  return `specs ${ordinals.slice(0, -1).join(", ")} and ${ordinals[ordinals.length - 1]}`;
}

function capital(text: string): string {
  return text.charAt(0).toUpperCase() + text.slice(1);
}

/** 2026-10-13-9a59r: the banner's way to the Specs tab. */
function OpenSpecsButton() {
  const address = useTicketTabGo();
  return <button type="button" className="btn" data-testid="ticket-open-specs" onClick={() => address.go("specs")}>Open specs</button>;
}
