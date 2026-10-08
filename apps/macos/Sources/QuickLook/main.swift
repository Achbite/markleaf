// QuickLook 扩展经 NSExtensionPrincipalClass 引导，链接入口由
// Package.swift 的 -e _NSExtensionMain 指定。较新的 Swift 工具链会为
// executable target 合成对 <模块名>_main 的引用；没有顶层入口文件时该
// 符号无人定义，链接失败。提供一个空的 main.swift 满足两类工具链，
// 实际初始化仍完全由扩展宿主按 PrincipalClass 完成。
