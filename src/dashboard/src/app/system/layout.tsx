import { SystemSectionNav } from "@/components/system/SystemSectionNav";
import { WithSectionNav } from "@/components/shell/SectionNav";

// 2026-11-16-0440c: the running system is reached through the profile, and carries its own sections.
export default function SystemLayout({ children }: { children: React.ReactNode }) {
  return <WithSectionNav nav={<SystemSectionNav />}>{children}</WithSectionNav>;
}
