// jsdom 未实现 ResizeObserver，编辑器图片 NodeView 依赖它监听容器尺寸变化。
// 在测试环境中用一个空实现替代，避免 `new ResizeObserver` 抛 ReferenceError。
class ResizeObserverStub {
  observe(): void {}

  unobserve(): void {}

  disconnect(): void {}
}

if (typeof globalThis.ResizeObserver === 'undefined') {
  globalThis.ResizeObserver = ResizeObserverStub as unknown as typeof ResizeObserver
}

// jsdom 的 Range 同样缺少几何 API。源码视图的阅读锚点经由 CodeMirror 的
// measureTextSize -> textRange().getClientRects() 读取行几何信息，缺失时
// requestSnapshot 会在测试中直接抛错。返回空矩形即可满足测量回退路径。
if (typeof Range !== 'undefined') {
  const emptyRectList = (): DOMRectList => ({
    length: 0,
    item: () => null,
    [Symbol.iterator]: function* () {},
  } as unknown as DOMRectList)

  if (typeof Range.prototype.getClientRects !== 'function') {
    Range.prototype.getClientRects = function getClientRects(): DOMRectList {
      return emptyRectList()
    }
  }

  if (typeof Range.prototype.getBoundingClientRect !== 'function') {
    Range.prototype.getBoundingClientRect = function getBoundingClientRect(): DOMRect {
      return new DOMRect(0, 0, 0, 0)
    }
  }
}

