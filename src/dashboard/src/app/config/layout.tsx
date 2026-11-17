import { ConfigSectionNav } from "@/components/config/ConfigSectionNav";
import { WithSectionNav } from "@/components/shell/SectionNav";

// 2026-11-16-0440c: configuration is reached through the profile, and carries its own sections.
export default function ConfigLayout({ children }: { children: React.ReactNode }) {
  return <WithSectionNav nav={<ConfigSectionNav />}>{children}</WithSectionNav>;
}
