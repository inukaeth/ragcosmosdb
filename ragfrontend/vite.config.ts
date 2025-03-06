import { defineConfig } from 'vite';
import plugin from '@vitejs/plugin-vue';

// https://vitejs.dev/config/
export default defineConfig({
    plugins: [plugin()],
    server: {
        port: 55240,
        proxy: {
            '/api': {
              target: ' http://localhost:7020',
              changeOrigin: true,         
            }
          }
    }
})


