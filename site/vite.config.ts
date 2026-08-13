import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";

// The URL does not change: the site stays at https://alegauss.github.io/dockerdesk/,
// so every canonical, asset path and (later) sitemap entry carries this prefix. (§6)
export const BASE = "/dockerdesk/";

export default defineConfig({
  base: BASE,
  plugins: [react(), tailwindcss()],
  build: {
    // docs/ is roadkeep's, never a web root — the site builds to its own dist/. (§6)
    outDir: "dist",
    emptyOutDir: true,
  },
});
