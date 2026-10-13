// 2026-10-13-9a59q: a ticket page's address, apart from the reads, so a module mock of the ticket API
// never takes the links with it. A ticket id may carry a slash; encoded, it stays one path segment and
// the server decodes it.

export function ticketPath(project: string, ticketId: string): string {
  return `/tickets/${encodeURIComponent(project)}/${encodeURIComponent(ticketId)}`;
}
