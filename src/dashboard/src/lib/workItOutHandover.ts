import type { PickLeftOut } from "@/components/dialog/referenceSelection";

// 2026-11-17-d6fba: the first message of a conversation started on the home page. The home composer collects it and
// Work it out sends it — through the same attach, attachSite and send the page's own composer uses — so there is one
// way a conversation starts. Files cannot travel in an address, so the hand-over is held in memory: a client-side
// navigation keeps it, a reload drops it, and it is taken exactly once.

export interface WorkItOutHandover {
  text: string;
  project: string;
  /** Images, uploaded one by one as the composer's image entry does. */
  images: File[];
  /** File or folder picks, each stored as one set with what its selection left out. */
  sets: Array<{ files: File[]; leftOut?: PickLeftOut }>;
}

let held: WorkItOutHandover | null = null;

/** Holds the first message for the Work it out page to send. */
export function handOver(handover: WorkItOutHandover): void {
  held = handover;
}

/** The held first message, once; null when there is none. */
export function takeHandover(): WorkItOutHandover | null {
  const taken = held;
  held = null;
  return taken;
}
