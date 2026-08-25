import { IdentityView } from "@/components/identity/IdentityView";

// 2026-08-25-4530: the route the app rail reaches, over p0503d's endpoint. Its
// own page rather than a system section: this is about the person reading the
// dashboard, not about a subsystem of the installation.
export default function IdentityPage() {
  return <IdentityView />;
}
