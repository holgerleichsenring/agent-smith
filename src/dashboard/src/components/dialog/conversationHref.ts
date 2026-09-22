import type { SpecDialogSessionSummary } from "@/types/spec-dialog";

// 2026-09-21-f237b: the address that hands a conversation to the surface.
//
// It carries the dialog id as well as the session id, and that is not redundancy. The hook's
// open() goes to a conversation's OWN dialog id only when its caller supplies one, and otherwise
// queues a resume — which the server refuses while a turn runs, which is the very case going to
// the dialog exists to serve. A session id alone would leave the surface unable to tell the two
// apart, and it could not look the dialog id up: nothing maps a session id to it except the
// conversation list, which is the read that may not hold the row. The page holds the row it is
// linking from, so it puts both on the address and the surface needs no lookup at all.

export function conversationHref({
  sessionId,
  openDialogId,
}: SpecDialogSessionSummary): string {
  const params = new URLSearchParams({ open: sessionId });
  if (openDialogId) params.set("on", openDialogId);
  return `/spec-dialog?${params.toString()}`;
}
