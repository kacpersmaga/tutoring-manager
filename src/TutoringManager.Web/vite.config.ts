import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// In development the API runs on its own port; in production the API serves this build from wwwroot.
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/api': 'http://localhost:5139',
      '/health': 'http://localhost:5139',
    },
  },
  build: {
    outDir: '../TutoringManager.Api/wwwroot',
    emptyOutDir: true,
  },
})
