"use client";

import { useEffect, useState } from "react";

// 2026-09-18-2f8b: the seconds since the running turn was last known to be computing, counted
// here rather than in the hook holding the conversation: a per-second interval up there would
// re-render the transcript, the panes and the filed work once a second for the length of a
// turn, to move one number. Here it re-renders one span, and stops when that unmounts.
//
// It counts up from a moment on THIS browser's clock, derived once from the seconds the server
// says the turn has computed. Two clocks are never differenced, so a browser running behind
// cannot render a turn that started in the future.

// 2026-11-13-3dc8: in minutes and seconds — "648s" asks the reader to divide.

/** Seconds as "Xm Ys". */
export function minutesAndSeconds(seconds: number): string {
  return `${Math.floor(seconds / 60)}m ${seconds % 60}s`;
}

/** How long the turn has been working, ticking once a second. */
export function DialogElapsed({ since }: { since: number }) {
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    const ticker = setInterval(() => setNow(Date.now()), 1000);
    return () => clearInterval(ticker);
  }, []);
  return <>{minutesAndSeconds(Math.max(0, Math.floor((now - since) / 1000)))}</>;
}
