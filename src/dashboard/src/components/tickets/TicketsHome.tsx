"use client";

import { useEffect, useState } from "react";
import { useTicketBucketFilter } from "@/lib/TicketBucketFilter";
import { TicketGraph } from "./graph/TicketGraph";
import { TicketList } from "./TicketList";

// 2026-10-14-dbcen: /tickets with its List | Graph switch (ticket-graph.html). The view is in the URL (?view=graph)
// so the graph is linkable, read after mount through the History API like the bucket filter; choosing a bucket
// in the rail drops the parameter and shows the list.

export const VIEW_PARAM = "view";
type View = "list" | "graph";

export function ticketViewOf(search: string): View {
  return new URLSearchParams(search).get(VIEW_PARAM) === "graph" ? "graph" : "list";
}

export function TicketsHome() {
  const [view, setView] = useState<View>("list");
  const { filter } = useTicketBucketFilter();
  useEffect(() => setView(ticketViewOf(window.location.search)), [filter]);
  useEffect(() => {
    const readUrl = () => setView(ticketViewOf(window.location.search));
    readUrl();
    window.addEventListener("popstate", readUrl);
    return () => window.removeEventListener("popstate", readUrl);
  }, []);
  const choose = (next: View) => {
    setView(next);
    window.history.pushState(null, "", next === "graph" ? `${window.location.pathname}?${VIEW_PARAM}=graph` : window.location.pathname);
  };
  return (
    <>
      <div className="m-head">
        <div>
          <h1>Tickets</h1>
          <div className="msub">
            {view === "graph"
              ? "Every ticket that requires or is required, an arrow from each ticket to what it requires."
              : "Every ticket agent-smith has worked, grouped by who has to act next. A ticket is the work; each run is one attempt at it."}
          </div>
        </div>
        <div className="viewsw" role="group" aria-label="View">
          {(["list", "graph"] as const).map((v) => (
            <button key={v} type="button" className="vs" aria-pressed={view === v} onClick={() => choose(v)} data-testid={`tickets-view-${v}`}>
              {v === "list" ? "List" : "Graph"}
            </button>
          ))}
        </div>
      </div>
      {view === "graph" ? <TicketGraph /> : <TicketList />}
    </>
  );
}
