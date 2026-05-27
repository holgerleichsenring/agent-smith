import type { Metadata } from "next";
import Link from "next/link";
import "./globals.css";

export const metadata: Metadata = {
  title: "agent-smith",
  description: "AI orchestration framework — self-hosted",
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en">
      <body>
        <nav className="border-b border-stone-200 bg-white px-6 py-3 text-sm">
          <div className="mx-auto flex max-w-6xl items-center gap-6">
            <Link href="/" className="font-medium text-stone-800 hover:text-stone-900">
              agent-smith
            </Link>
            <Link href="/" className="text-stone-600 hover:text-stone-900">
              Runs
            </Link>
            <Link href="/system" className="text-stone-600 hover:text-stone-900">
              System
            </Link>
          </div>
        </nav>
        {children}
      </body>
    </html>
  );
}
