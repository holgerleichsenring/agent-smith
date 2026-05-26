import type { Config } from "tailwindcss";
import { readFileSync, existsSync } from "node:fs";
import { resolve } from "node:path";

const tokensPath = resolve(__dirname, "src/styles/tokens.json");
const tokens = existsSync(tokensPath)
  ? (JSON.parse(readFileSync(tokensPath, "utf8")) as {
      colors: Record<string, string>;
      fontFamily: Record<string, string[]>;
      borderRadius: Record<string, string>;
      spacing: Record<string, string>;
    })
  : { colors: {}, fontFamily: {}, borderRadius: {}, spacing: {} };

const config: Config = {
  content: ["./src/**/*.{ts,tsx}"],
  darkMode: "media",
  theme: {
    extend: {
      colors: tokens.colors,
      fontFamily: tokens.fontFamily,
      borderRadius: tokens.borderRadius,
      spacing: tokens.spacing,
    },
  },
  plugins: [],
};

export default config;
