import path from "node:path";
import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig({
  plugins: [react()],
  base: "/admin/",
  build: {
    outDir: path.resolve(__dirname, "../wwwroot/admin"),
    emptyOutDir: true
  },
  server: {
    port: 5173,
    proxy: {
      "/admin/api": {
        target: "http://localhost:5000",
        changeOrigin: true
      }
    }
  },
  test: {
    environment: "jsdom",
    globals: true,
    setupFiles: path.resolve(__dirname, "src/test/setup.ts")
  }
});
