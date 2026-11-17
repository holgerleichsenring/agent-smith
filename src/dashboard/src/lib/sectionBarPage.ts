import { isTicketPage } from "@/lib/tickets/ticketAddress";

// 2026-11-17-d762a: a page that brings its own section bar beside the app sidebar — one ticket's page (8d1ca) and
// Work it out, whose conversations are its bar. The sidebar shows icons only beside one, so the two bars fit.

export const WORK_IT_OUT_PAGE = "/spec-dialog";

/** Whether the page at this path draws its own section bar. The conversations list under it does not. */
export function hasSectionBar(pathname: string): boolean {
  return isTicketPage(pathname) || pathname === WORK_IT_OUT_PAGE;
}
