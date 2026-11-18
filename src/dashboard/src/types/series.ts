// 2026-11-17-0207e: a series run as the Work it out page's panel reads it (GET /api/spec-dialog/{dialogId}/series).

export interface SeriesNowRow {
  ticket: string;
  text: string;
  /** running · green · red · waiting · held */
  kind: string;
}

export interface SeriesRunView {
  id: string;
  /** running · verifying · done · stopped */
  state: string;
  /** "series running", "series running · 1 question for you", "series done", … */
  headState: string;
  landed: number;
  total: number;
  elapsed: string;
  cost: string;
  /** Null until two tickets landed — before that there is nothing to measure by. */
  eta: string | null;
  now: SeriesNowRow[];
  criticalPath: string[];
  criticalPathLeft: string | null;
  policy: string;
  question: string | null;
}
