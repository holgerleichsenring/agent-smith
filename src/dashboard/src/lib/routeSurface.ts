import type { TextKey } from "@/lib/i18n/translate";

// 2026-08-25-39ab: the operator's name for a route. A boundary that says "the
// /jobs/9f3c-… page failed" names a URL; one that says "the run view could not
// be rendered" names the thing the operator was looking at. The route error
// boundary is the only caller — the mapping lives here so it is testable
// without rendering a route.

const SURFACE_BY_SEGMENT: Record<string, TextKey> = {
  "": "shell.surface.home",
  jobs: "shell.surface.runView",
  config: "shell.surface.configuration",
  system: "shell.surface.systemView",
  "pull-requests": "shell.surface.pullRequests",
  tickets: "shell.surface.ticketList",
  delivery: "shell.surface.delivery",
};

/** The dictionary key of the surface a path renders (2026-11-23-5669a: a key, worded in the language in force). */
export function routeSurfaceName(pathname: string | null | undefined): TextKey {
  const segment = (pathname ?? "").split("/").filter((part) => part.length > 0)[0] ?? "";
  return SURFACE_BY_SEGMENT[segment] ?? "shell.surface.page";
}
