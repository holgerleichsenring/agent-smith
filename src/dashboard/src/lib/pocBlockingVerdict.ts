import type { PocRevisionView, PocScanVerdict } from "@/lib/pocsApi";

/** 2026-11-23-933dd: the verdict a blocked revision is blocked by — the image scan's at stage image, else the code scan's. */
export function blockingVerdict(view: PocRevisionView): PocScanVerdict | null {
  return view.blockedStage === "image" ? view.imageVerdict ?? null : view.verdict;
}
