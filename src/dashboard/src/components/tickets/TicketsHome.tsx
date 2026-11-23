"use client";

import { TicketList } from "./TicketList";
import { TicketStatePills } from "./TicketStatePills";

// 2026-10-14-dbcen: /tickets. 2026-11-16-0440b: the buckets are pills above the list. 2026-11-23-849e: the List | Graph
// switch is gone — a series page's Order and branches draws what requires what, so the page is the list alone.

export function TicketsHome() {
  return (
    <>
      <div className="m-head">
        <div>
          <h1>Tickets</h1>
          <div className="msub">
            Every ticket agent-smith has worked, grouped by who has to act next. A ticket is the work; each run is one attempt at it.
          </div>
        </div>
      </div>
      <TicketStatePills />
      <TicketList />
    </>
  );
}
