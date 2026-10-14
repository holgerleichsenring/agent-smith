"use client";

import { Fragment } from "react";
import { useAttemptMachines, useTicketConversation, useTicketDetail, useTicketSeries } from "@/lib/tickets/TicketPageStore";
import { ticketPath } from "@/lib/tickets/ticketAddress";
import type { TicketAttempt } from "@/types/tickets";
import { AttemptCard } from "./AttemptCard";
import { causeLabel, causeWhere } from "./attemptStory";
import { clock } from "./flowWords";
import { CAUSE_PARAM, TAB_PARAM, useTicketTabGo } from "./useTicketTabGo";

// 2026-10-13-9a59s: the Attempts tab — what started each attempt, what it did, why it stopped. Newest first
// by run id; between two cards the change that started the newer one, whose read opens the Conversation tab
// at that cause (9a59r's ?causeOf= join) when the cause names a source entry. The next attempt starts from the
// conversation; nothing here starts one.

export function TicketAttemptsTab() {
  const detail = useTicketDetail();
  const series = useTicketSeries();
  const conversation = useTicketConversation();
  const address = useTicketTabGo();
  const attempts = [...(detail.data?.attempts ?? [])].sort((l, r) => (l.runId < r.runId ? 1 : l.runId > r.runId ? -1 : 0));
  const machines = useAttemptMachines(attempts.map((a) => a.runId));
  const entries = conversation.data?.entries ?? [];
  const now = Date.now();

  return (
    <>
      <div className="next-card" data-testid="attempts-next">
        <span className="bi" aria-hidden="true">i</span>
        <div>
          <div className="bt">The next attempt starts from the conversation.</div>
          <div className="bs">
            Say what should change; approving the revised series there starts the next attempt with that conversation as
            its cause. An attempt that ended on infrastructure also offers <b>Retry unchanged</b> there.
          </div>
        </div>
        <button className="btn primary" type="button" data-testid="attempts-continue"
          onClick={() => address.go("conversation", { [CAUSE_PARAM]: null })}>Continue in conversation</button>
      </div>

      <div className="section-head first">
        <h2>Attempts</h2><span className="cnt">{attempts.length}</span>
        <span className="sh-sub">newest first · what started it, what it did, why it stopped</span>
      </div>
      <div className="tl" data-testid="attempts-timeline">
        {attempts.map((attempt, i) => (
          <Fragment key={attempt.runId}>
            <AttemptCard attempt={attempt} now={now} series={series.data}
              passes={machines.passes[attempt.runId]} compute={machines.compute[attempt.runId]} run={machines.run[attempt.runId]}
              cutEntries={entries.filter((e) => e.source === "spec-cut" && e.kind === "cut-review" && e.attemptRunId === attempt.runId)} />
            {i + 1 < attempts.length && <ChangeEvent attempt={attempt} project={detail.project} ticketId={detail.ticketId} />}
          </Fragment>
        ))}
      </div>
    </>
  );
}

/** The change that started `attempt`, drawn below its card and above the attempt before it. */
function ChangeEvent({ attempt, project, ticketId }: { attempt: TicketAttempt; project: string; ticketId: string }) {
  const address = useTicketTabGo();
  const label = causeLabel(attempt);
  if (!label) return null;
  const who = [attempt.cause?.actor, causeWhere(attempt)].filter(Boolean).join(" on ");
  const anchored = !!attempt.cause?.source?.id;
  const extra = { [CAUSE_PARAM]: anchored ? attempt.runId : null };
  const query = new URLSearchParams({ [TAB_PARAM]: "conversation", ...(anchored ? { [CAUSE_PARAM]: attempt.runId } : {}) });
  return (
    <div className="chg" data-testid={`attempt-change-${attempt.number}`}>
      <span className="dot" />
      <div className="line">
        {attempt.cause?.at && <span className="w">{clock(attempt.cause.at)}</span>}
        <b>{label[0].toUpperCase() + label.slice(1)}</b>
        {who && <span>{who}</span>}
        <a href={`${ticketPath(project, ticketId)}?${query.toString()}`} data-testid={`attempt-change-read-${attempt.number}`}
          onClick={(e) => {
            e.preventDefault();
            address.go("conversation", extra);
          }}>read</a>
      </div>
    </div>
  );
}
