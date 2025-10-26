import vue from '@vitejs/plugin-vue'
import { resolve } from 'path'
import { defineConfig } from 'vite'

export default defineConfig({
    base: "./",
    plugins: [vue()],
    resolve: {
        alias: {
            '@': resolve(__dirname, 'src'),
        },
    },
    server: {
        port: 3000,
        open: true
    },
    build: {
        outDir: 'dist',
        sourcemap: true
    }
})
