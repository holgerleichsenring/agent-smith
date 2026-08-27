import { redirect } from "next/navigation";

// 2026-08-27-1ed6: the connection check moved under /config. Not to /config/connections —
// that path lists the connection CATALOG — so the check keeps its own name there.
export default function ConnectionsRedirect() {
  redirect("/config/connection-check");
}
