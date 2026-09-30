import { fileURLToPath } from 'node:url'
import { defineConfig } from 'vitest/config'
import { katexWoff2Only, katexSelfContainedCss, sharedRendererDistribution } from '@markleaf/editor-core/build'

// macOS 与 Windows 宿主消费本包产物；VS Code 扩展的 webview 由
// apps/vscode/webview 独立构建，二者共享 @markleaf/editor-core。
export default defineConfig(({ mode }) => ({
  base: './',
  plugins: mode === 'test' ? [katexWoff2Only(), katexSelfContainedCss()] : [sharedRendererDistribution()],
  build: {
    outDir: 'dist',
    emptyOutDir: true,
    // 发布包不携带调试映射文件；开发调试可通过临时覆盖此项开启。
    sourcemap: false,
    chunkSizeWarningLimit: 550,
  },
  test: {
    alias: [{ find: /^@markleaf\/editor-core$/, replacement: fileURLToPath(new URL('../editor-core/src/index.ts', import.meta.url)) }],
    environment: 'jsdom',
    setupFiles: ['./vitest.setup.ts'],
    // 每条用例都要在 jsdom 里引导完整编辑器，慢机器或并行负载下默认 5s 会抖动超时。
    testTimeout: 30_000,
  },
}))
