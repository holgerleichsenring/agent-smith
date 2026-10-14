import { TicketList } from "@/components/tickets/TicketList";

// 2026-10-13-9a59p: the ticket list — ticket-view.html's list view under .mock-shell.mock-tickets.
// A ticket is the work; each run is one attempt at it. The runs home at / stays the machine view.
export default function TicketsPage() {
  return (
    <div className="mock-shell mock-tickets" data-testid="tickets-home">
      <main className="main">
        <div className="m-head">
          <div>
            <h1>Tickets</h1>
            <div className="msub">
              Every ticket agent-smith has worked, grouped by who has to act next. A ticket is the
              work; each run is one attempt at it.
            </div>
          </div>
        </div>
        <TicketList />
      </main>
    </div>
  );
}
