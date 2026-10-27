import { readFileSync } from "node:fs";

const site = JSON.parse(readFileSync(new URL("./src/_data/site.json", import.meta.url), "utf8"));

// Analytics, its consent banner and the privacy page exist only once site.json names the controller
// (site.legal.name and site.legal.email): a banner needs a privacy policy, and that needs a controller.
// The imprint also needs site.legal.address, since § 5 DDG asks for a postal address; without one it is
// withheld rather than published incomplete.
const filled = (value) => typeof value === "string" && value.trim() !== "";
export const legalState = (data) => {
  const analytics = Boolean(filled(data.analyticsId) && data.legal && filled(data.legal.name) && filled(data.legal.email));
  return { analytics, imprint: analytics && filled(data.legal.address) };
};
const state = legalState(site);

export default function (eleventyConfig) {
  eleventyConfig.addPassthroughCopy({ "src/static": "." });

  eleventyConfig.addGlobalData("legalReady", state.analytics);
  eleventyConfig.addGlobalData("imprintReady", state.imprint);
  if (state.analytics) {
    console.log("analytics on: consent banner and /privacy/ rendered");
  } else {
    console.log("analytics off: legal details missing in site.json");
    eleventyConfig.ignores.add("src/consent.js.njk");
    eleventyConfig.ignores.add("src/privacy.njk");
  }
  if (state.analytics && !state.imprint) console.log("imprint off: no postal address in site.json");
  if (!state.imprint) eleventyConfig.ignores.add("src/imprint.njk");

  // Add custom filter for pad
  eleventyConfig.addFilter("pad", (num, size) => String(num).padStart(size || 2, "0"));

  // vercel.json sets trailingSlash false: /services/ answers at /services, so that is the canonical path.
  eleventyConfig.addFilter("canonicalPath", (url) => (url === "/" ? "/" : String(url).replace(/\/$/, "")));

  return {
    dir: {
      input: "src",
      includes: "_includes",
      data: "_data",
      output: "_site"
    },
    templateFormats: ["njk"],
    htmlTemplateEngine: "njk"
  };
}
