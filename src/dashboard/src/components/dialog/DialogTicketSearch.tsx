"use client";

import { useT } from "@/hooks/useT";
import { useEffect, useRef, useState } from "react";

import { searchTickets, type TicketSearchFound } from "@/lib/specDialogApi";

// 2026-09-27-5c1eb: starting a conversation FROM a ticket without arriving with one in the address.
//
// It sits in the exchange column above the branch that swaps the project choice for the transcript,
// NOT inside the choice — an installation with exactly one configured project never renders that
// choice at all, so a field inside it would be invisible to most installations, and on a
// multi-project one the choice unmounts the moment a project is picked, taking any typed text and
// in-flight request with it.
//
// No combo box over every ticket: a board has thousands and a picker that lists them is a list, not
// a search. Results only, from the third character.

/** How long after the last keystroke the trackers are asked. Two of the four are rate limited far
 *  below the rest of their API, and every query costs one round trip PER configured tracker. */
const DEBOUNCE_MS = 300;

/** Characters before any tracker is asked. The SERVER holds the same floor and refuses below it —
 *  this copy exists because the field has to decide whether to ask before it has an answer to read
 *  a minimum off. The answer carries the server's own, so a disagreement is visible rather than
 *  silent: the page would ask and be refused. */
export const TICKET_SEARCH_MINIMUM = 3;

// 2026-09-27-481bf: an icon only where every tracker means the same thing by the word; everything
// else keeps the word with no mark, because Azure DevOps process templates and Jira projects both
// define their own types and a fixed set over an open vocabulary either mislabels or shows nothing.
const KIND_ICON: Record<string, string> = {
  bug: "\u{1F41E}",
  task: "\u2713",
  epic: "\u25C6",
};

function TicketKind({ kind }: { kind: string }) {
  const icon = KIND_ICON[kind.trim().toLowerCase()];
  return (
    <span data-testid={`dialog-ticket-kind-${kind.trim().toLowerCase()}`} className="ec-sub">
      {icon ? `${icon} ` : ""}
      {kind}
    </span>
  );
}

export function DialogTicketSearch({
  minimum = TICKET_SEARCH_MINIMUM,
  stranded = false,
  bound,
  picked,
  onPicked,
  reason,
}: {
  minimum?: number;
  /** 2026-09-27-481bb: the picked ticket's tracker has no configured project, so nothing here can
   *  bind it correctly. Said rather than papered over with a list of projects on other boards. */
  stranded?: boolean;
  /** The ticket ids that already have a conversation, so a row can say so rather than opening a
   *  second one the unique index would refuse anyway. */
  bound: ReadonlySet<string>;
  picked: TicketSearchFound | null;
  onPicked: (found: TicketSearchFound | null) => void;
  /** Why a ticket the page arrived with did not name one project. Shown here because this is the
   *  one place on the surface that renders whether or not a project is being chosen. */
  reason?: string | null;
}) {
  const t = useT();
  const [text, setText] = useState("");
  const [found, setFound] = useState<TicketSearchFound[] | null>(null);
  const [more, setMore] = useState(false);
  const [unsearchable, setUnsearchable] = useState<string[]>([]);
  const [unreachable, setUnreachable] = useState<string[]>([]);
  const [asking, setAsking] = useState(false);
  const [failed, setFailed] = useState(false);
  const typed = text.trim();

  // The request in flight is ABORTED on the next keystroke, and the debounce is cleared with it:
  // a person typing eight characters must not leave eight sweeps of every configured tracker
  // running, and an older answer must not land on top of a newer one.
  const asked = useRef<AbortController | null>(null);
  useEffect(() => {
    asked.current?.abort();
    if (typed.length < minimum) {
      setFound(null);
      setAsking(false);
      setFailed(false);
      return;
    }
    const controller = new AbortController();
    asked.current = controller;
    setAsking(true);
    const timer = window.setTimeout(() => {
      void searchTickets(typed, controller.signal)
        .then((read) => {
          setFound(read.found);
          setMore(read.moreHeldBack);
          setUnsearchable(read.unsearchable);
          setUnreachable(read.unreachable);
          setFailed(false);
        })
        .catch(() => {
          if (controller.signal.aborted) return;
          setFailed(true);
          setFound([]);
        })
        .finally(() => {
          if (!controller.signal.aborted) setAsking(false);
        });
    }, DEBOUNCE_MS);
    return () => {
      window.clearTimeout(timer);
      controller.abort();
    };
  }, [typed, minimum]);

  return (
    <div data-testid="dialog-ticket-search" className="flex flex-col gap-2">
      {reason && (
        <p data-testid="dialog-ticket-reason" className="dsh-body text-body">
          {reason}
        </p>
      )}
      {picked ? (
        <p data-testid="dialog-ticket-picked" className="ec-marks ec-sub items-center">
          <span className="ec-mark filed">{picked.ticketId}</span>
          {picked.kind && <TicketKind kind={picked.kind} />}
          <span className="min-w-0 truncate text-ink">{picked.title}</span>
          <span>{t("dialog.ticket.on", { tracker: picked.tracker })}</span>
          {stranded && (
            <span data-testid="dialog-ticket-stranded" className="ec-mark">
              {t("dialog.ticket.stranded", { tracker: picked.tracker })}
            </span>
          )}
          <button
            type="button"
            data-testid="dialog-ticket-clear"
            onClick={() => {
              onPicked(null);
              setText("");
            }}
            className="d-conv-x"
            aria-label={t("dialog.ticket.clear")}
          >
            ×
          </button>
        </p>
      ) : (
        <input
          data-testid="dialog-ticket-query"
          aria-label={t("dialog.ticket.query")}
          placeholder={t("dialog.ticket.placeholder", { count: minimum })}
          value={text}
          onChange={(event) => setText(event.target.value)}
          className="d-input"
        />
      )}
      {!picked && typed.length > 0 && typed.length < minimum && (
        <p data-testid="dialog-ticket-minimum" className="ec-sub">
          {t("dialog.ticket.minimum", { count: minimum })}
        </p>
      )}
      {!picked && asking && found === null && (
        <p data-testid="dialog-ticket-asking" className="ec-sub">
          {t("dialog.ticket.asking")}
        </p>
      )}
      {!picked && found?.length === 0 && !failed && (
        <p data-testid="dialog-ticket-none" className="ec-sub">
          {t("dialog.ticket.none")}
        </p>
      )}
      {!picked && failed && (
        <p data-testid="dialog-ticket-failed" className="ec-sub">
          {t("dialog.ticket.failed")}
        </p>
      )}
      {!picked && found !== null && found.length > 0 && (
        <ul data-testid="dialog-ticket-results" className="flex flex-col gap-1">
          {found.map((hit) => (
            <li key={`${hit.tracker}/${hit.ticketId}`}>
              <button
                type="button"
                data-testid={`dialog-ticket-hit-${hit.ticketId}`}
                onClick={() => onPicked(hit)}
                className="d-conv w-full text-left"
              >
                <span className="line-clamp-2 dsh-body font-medium text-ink">{hit.title}</span>
                <span className="ec-marks ec-sub items-center">
                  <span className="ec-mark given">{hit.ticketId}</span>
                  {hit.kind && <TicketKind kind={hit.kind} />}
                  {/* 2026-09-27-481bb: the number that was typed, rather than a ticket whose text
                      merely mentions it — it was already first, and now it says so. */}
                  {hit.exact && (
                    <span data-testid={`dialog-ticket-exact-${hit.ticketId}`} className="ec-mark filed">
                      {t("dialog.ticket.exact")}
                    </span>
                  )}
                  <span>{hit.tracker}</span>
                  {bound.has(hit.ticketId) && (
                    <span data-testid={`dialog-ticket-bound-${hit.ticketId}`} className="ec-mark filed">
                      {t("dialog.ticket.bound")}
                    </span>
                  )}
                </span>
              </button>
            </li>
          ))}
        </ul>
      )}
      {/* A cap that does not say so is a lie, and a tracker that could not answer is not an
          empty board — both are the reason this page has its own search rather than a list. */}
      {!picked && more && (
        <p data-testid="dialog-ticket-more" className="ec-sub">
          {t("dialog.ticket.more")}
        </p>
      )}
      {/* A tracker whose TEXT search worked and whose NUMBER read failed is not one nothing is
          known about — two failures, two sentences. */}
      {!picked && unreachable.length > 0 && (
        <p data-testid="dialog-ticket-unreachable" className="ec-sub">
          {t("dialog.ticket.unreachable", { count: unreachable.length, list: unreachable.join(", ") })}
        </p>
      )}
      {!picked && unsearchable.length > 0 && (
        <p data-testid="dialog-ticket-unsearchable" className="ec-sub">
          {t("dialog.ticket.unsearchable", { count: unsearchable.length, list: unsearchable.join(", ") })}
        </p>
      )}
    </div>
  );
}
