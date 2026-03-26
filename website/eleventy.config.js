export default function (eleventyConfig) {
  eleventyConfig.addPassthroughCopy({ "src/static": "." });

  // Add custom filter for pad
  eleventyConfig.addFilter("pad", (num, size) => String(num).padStart(size || 2, "0"));

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
