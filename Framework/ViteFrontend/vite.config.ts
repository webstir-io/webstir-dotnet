import { defineConfig } from 'vite';

export default defineConfig({
  root: 'src/frontend',
  build: {
    outDir: '../../build/frontend',
    emptyOutDir: true,
    rollupOptions: {
      input: 'src/frontend/main.ts'
    }
  }
});
