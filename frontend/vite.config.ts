import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import { VitePWA } from 'vite-plugin-pwa'

export default defineConfig({
  plugins: [
    react(),
    tailwindcss(),
    VitePWA({
      registerType: 'autoUpdate',
      strategies: 'injectManifest',
      srcDir: 'src/service-worker',
      filename: 'sw.ts',
      injectRegister: false,
      manifest: {
        name: 'TeslaTracker',
        short_name: 'TeslaTracker',
        description: 'Spåra din Tesla-orderstatus',
        theme_color: '#000000',
        background_color: '#0a0a0a',
        display: 'standalone',
        start_url: '/',
        icons: [],
      },
      injectManifest: {
        injectionPoint: undefined,
      },
      devOptions: {
        enabled: true,
        type: 'module',
      },
    }),
  ],
  test: {
    globals: true,
    environment: 'jsdom',
    setupFiles: ['./src/test-setup.ts'],
  },
})
