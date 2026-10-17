// 2026-10-17-e730e: a scroll the reader asked for glides, unless the system asks for reduced motion.

/** "smooth", or "auto" when prefers-reduced-motion is reduce (or cannot be read). */
export function scrollBehavior(): ScrollBehavior {
  if (typeof window === "undefined" || typeof window.matchMedia !== "function") return "auto";
  return window.matchMedia("(prefers-reduced-motion: reduce)").matches ? "auto" : "smooth";
}
