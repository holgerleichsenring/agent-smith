"use client";

import { Suspense, use } from "react";
import { TicketPage } from "@/components/tickets/detail/TicketPage";
import { TicketPageStore } from "@/lib/tickets/TicketPageStore";

// 2026-10-13-9a59q: one ticket — ticket-view.html's detail view. The page's ONE store holds every read
// of the ticket; ?tab= is read under this Suspense boundary, never in the root layout.

interface PageProps {
  params: Promise<{ project: string; ticketId: string }>;
}

export default function TicketDetailRoute({ params }: PageProps) {
  const { project, ticketId } = use(params);
  const decodedProject = decodeURIComponent(project);
  const decodedTicket = decodeURIComponent(ticketId);
  return (
    <TicketPageStore key={`${decodedProject}/${decodedTicket}`} project={decodedProject} ticketId={decodedTicket}>
      <Suspense fallback={null}>
        <TicketPage />
      </Suspense>
    </TicketPageStore>
  );
}
