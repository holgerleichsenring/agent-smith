import { redirect } from "next/navigation";

// 2026-08-27-1ed6: the installation read-out moved under /config. A bookmark, a link in a
// ticket and the 729e release line all still land on it.
export default function InstallationRedirect() {
  redirect("/config/installation");
}
