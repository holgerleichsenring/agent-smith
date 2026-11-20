"use client";

import { createContext, useContext } from "react";

// 2026-11-19-1b72c: replying on the ticket page goes through the composer, which knows whether a decision stands in
// its place. A reply sets that decision aside the way Keep chatting does, then focuses the text field; the Your move
// box and the divergence notice both reply through it rather than focusing a field that may not be mounted.

export type TicketReply = () => void;

export const TicketReplyContext = createContext<TicketReply | null>(null);

/** The thread's reply; null outside a thread (a never-run ticket has no composer). */
export function useTicketReply(): TicketReply | null {
  return useContext(TicketReplyContext);
}
