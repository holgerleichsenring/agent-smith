import { TicketsHome } from "@/components/tickets/TicketsHome";

// 2026-10-13-9a59p: the ticket list — ticket-view.html's list view under .mock-shell.mock-tickets.
// A ticket is the work; each run is one attempt at it. The runs home at / stays the machine view.
// 2026-10-14-dbcen: with a List | Graph switch (ticket-graph.html).
export default function TicketsPage() {
  return (
    <div className="mock-shell mock-tickets" data-testid="tickets-home">
      <main className="main">
        <TicketsHome />
      </main>
    </div>
  );
}
