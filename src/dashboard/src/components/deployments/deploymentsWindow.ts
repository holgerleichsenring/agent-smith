// 2026-11-23-8df6a: the Deployments page's time span — 7, 30 or 90 days, named by ?days= in the address; anything else
// reads as the default 7, which the address leaves out.

export const DEPLOYMENT_WINDOWS = [7, 30, 90] as const;
export type DeploymentWindow = (typeof DEPLOYMENT_WINDOWS)[number];
export const DEFAULT_WINDOW: DeploymentWindow = 7;
export const DAYS_PARAM = "days";

export function windowOf(param: string | null): DeploymentWindow {
  const days = Number(param);
  return DEPLOYMENT_WINDOWS.find((w) => w === days) ?? DEFAULT_WINDOW;
}

/** The page's address for a window: the default one has no query. */
export function windowHref(path: string, days: DeploymentWindow): string {
  return days === DEFAULT_WINDOW ? path : `${path}?${DAYS_PARAM}=${days}`;
}
