import path from "node:path";
import { defineConfig, loadEnv } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, __dirname, "");
  const backendUrl = env.VITE_BACKEND_URL || "http://localhost:5030";

  return {
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
          target: backendUrl,
          changeOrigin: true,
          secure: false,
          cookieDomainRewrite: "localhost"
        }
      }
    },
    test: {
      environment: "jsdom",
      globals: true,
      setupFiles: path.resolve(__dirname, "src/test/setup.ts")
    }
  };
});
