// 2026-08-25-39ab: the operator's name for a route. A boundary that says "the
// /jobs/9f3c-… page failed" names a URL; one that says "the run view could not
// be rendered" names the thing the operator was looking at. The route error
// boundary is the only caller — the mapping lives here so it is testable
// without rendering a route.

const SURFACE_BY_SEGMENT: Record<string, string> = {
  "": "run monitor",
  jobs: "run view",
  config: "configuration",
  system: "system view",
  "pull-requests": "pull requests",
};

export function routeSurfaceName(pathname: string | null | undefined): string {
  const segment = (pathname ?? "").split("/").filter((part) => part.length > 0)[0] ?? "";
  return SURFACE_BY_SEGMENT[segment] ?? "page";
}
