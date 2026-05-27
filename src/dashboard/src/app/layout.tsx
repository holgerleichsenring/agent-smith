import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "agent-smith",
  description: "AI orchestration framework — self-hosted",
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en">
      <body>{children}</body>
    </html>
  );
}
