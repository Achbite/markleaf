import Foundation

enum SaveAllPolicy {
    static func targets(tabs: [DocumentTab]) -> [DocumentTabID] {
        tabs.filter { $0.isDirty && $0.path != nil }.map(\.tabID)
    }

    static func hasTarget(tabs: [DocumentTab]) -> Bool {
        tabs.contains { $0.isDirty && $0.path != nil }
    }
}
