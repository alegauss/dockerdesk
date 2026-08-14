import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";

// GitHub Pages derives this from the repository name, so it is not a preference — the site
// is served at https://alegauss.github.io/freewilly/ and every canonical, asset path and
// sitemap entry carries the prefix. (§6, DD59)
export const BASE = "/freewilly/";

export default defineConfig({
  base: BASE,
  plugins: [react(), tailwindcss()],
  build: {
    // docs/ is roadkeep's, never a web root — the site builds to its own dist/. (§6)
    outDir: "dist",
    emptyOutDir: true,
  },
});
