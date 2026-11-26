import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// В разработке фронтенд и API живут на разных портах, а прокси делает их «одним сайтом»:
// запросы /api и /hubs уходят в ASP.NET Core, поэтому CORS не нужен ни здесь, ни в Docker,
// где собранный фронтенд отдаёт сам API.
const api = process.env.RESERVO_API ?? 'http://localhost:5006';

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/api': { target: api, changeOrigin: true },
      '/hubs': { target: api, changeOrigin: true, ws: true },
    },
  },
  build: {
    outDir: 'dist',
    sourcemap: false,
  },
});
