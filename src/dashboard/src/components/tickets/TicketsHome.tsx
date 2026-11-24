"use client";

import { useT } from "@/hooks/useT";
import { TicketList } from "./TicketList";
import { TicketStatePills } from "./TicketStatePills";

// 2026-10-14-dbcen: /tickets. 2026-11-16-0440b: the buckets are pills above the list. 2026-11-23-849e: the List | Graph
// switch is gone — a series page's Overview graph draws what requires what (2026-11-23-f562), so the page is the list alone.

export function TicketsHome() {
  const t = useT();
  return (
    <>
      <div className="m-head">
        <div>
          <h1>{t("tickets.home.title")}</h1>
          <div className="msub">
            {t("tickets.home.sub")}
          </div>
        </div>
      </div>
      <TicketStatePills />
      <TicketList />
    </>
  );
}
