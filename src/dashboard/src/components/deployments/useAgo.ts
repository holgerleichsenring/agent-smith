import { useT } from "@/hooks/useT";

// 2026-11-23-8df6a: how long ago a time was, short, in the language in force.

export function useAgo(): (iso: string) => string {
  const t = useT();
  return (iso: string) => {
    const then = new Date(iso).getTime();
    if (Number.isNaN(then)) return "";
    const minutes = Math.floor(Math.max(0, Date.now() - then) / 60_000);
    if (minutes < 1) return t("deployments.ago.justNow");
    if (minutes < 60) return t("deployments.ago.minutes", { count: minutes });
    const hours = Math.floor(minutes / 60);
    if (hours < 24) return t("deployments.ago.hours", { count: hours });
    return t("deployments.ago.days", { count: Math.floor(hours / 24) });
  };
}
