import { defineConfig } from 'vitest/config'
import { katexSelfContainedCss, katexWoff2Only } from './build/index'

// 此配置只服务于内核自身的契约测试。
// 生产产物由 vite.document.config.ts 与 vite.renderer.config.ts 独立构建。
export default defineConfig({
  plugins: [katexWoff2Only(), katexSelfContainedCss()],
  test: {
    environment: 'jsdom',
    setupFiles: ['./vitest.setup.ts'],
    // 每条用例都要在 jsdom 里引导完整编辑器，慢机器或并行负载下默认 5s 会抖动超时。
    testTimeout: 30_000,
  },
})
