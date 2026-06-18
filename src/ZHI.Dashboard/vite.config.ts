import { fileURLToPath, URL } from 'node:url';
import { defineConfig } from 'vite';
import tailwindcss from '@tailwindcss/vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [tailwindcss(), react()],
  root: fileURLToPath(new URL('.', import.meta.url)),
  server: {
    open: false,
    port: 5173,
    proxy: {
      '/api': 'http://localhost:8088',
      '/ws': {
        target: 'ws://localhost:8088',
        ws: true,
      },
    },
  },
  build: {
    outDir: fileURLToPath(new URL('../../src/ZHI.Watcher/wwwroot', import.meta.url)),
  },
  resolve: {
    alias: {
      '@components': fileURLToPath(new URL('./src/components', import.meta.url)),
      '@hooks': fileURLToPath(new URL('./src/hooks', import.meta.url)),
      '@i18n': fileURLToPath(new URL('./src/i18n', import.meta.url)),
      '@css': fileURLToPath(new URL('./src/styles', import.meta.url)),
      '@utils': fileURLToPath(new URL('./src/utils', import.meta.url)),
    },
  },
});
