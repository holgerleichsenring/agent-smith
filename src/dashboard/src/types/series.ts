// 2026-11-17-0207e: a series run as the Work it out page's panel reads it (GET /api/spec-dialog/{dialogId}/series).

/** 2026-11-19-1b72a: one ticket of the series as the pane lists it — every ticket, not only what moves. */
export interface SeriesTicketRow {
  ticket: string;
  what: string;
  state: string;
  /** landed · running · green · red · waiting · held · not started */
  kind: string;
  cost: string;
  time: string;
}

/** 2026-11-19-1b72a: what the conversation head's button asks of the series. */
export type SeriesAct = "pause" | "resume";

export interface SeriesRunView {
  id: string;
  /** running · verifying · paused · done · stopped */
  state: string;
  /** "series running", "series running · 1 question for you", "series done", … */
  headState: string;
  landed: number;
  total: number;
  elapsed: string;
  cost: string;
  /** Null until two tickets landed — before that there is nothing to measure by. */
  eta: string | null;
  tickets: SeriesTicketRow[];
  criticalPath: string[];
  criticalPathLeft: string | null;
  policy: string;
  question: string | null;
}
