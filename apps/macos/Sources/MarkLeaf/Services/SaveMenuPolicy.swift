import Foundation

/// Keeps Save commands aligned with the work their actions would actually do.
enum SaveMenuPolicy {
    static func isSaveEnabled(hasSession: Bool, isDirty: Bool, isReadOnly: Bool) -> Bool {
        hasSession && isDirty && !isReadOnly
    }

    static func isSaveAllEnabled(hasSavableTarget: Bool) -> Bool {
        hasSavableTarget
    }
}
