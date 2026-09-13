#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")/../.." && pwd)"
MENU="$ROOT_DIR/Sources/MarkLeaf/Support/NativeMenuBuilder.swift"
WINDOW="$ROOT_DIR/Sources/MarkLeaf/Views/EditorWindowController.swift"

fail() {
    echo "FAIL: $1" >&2
    exit 1
}

require() {
    local file="$1" text="$2" message="$3"
    grep -Fq "$text" "$file" || fail "$message"
}

# The File → Open command must use one window-level entry point so its target can
# honor the external-file preference.  Routing an active session straight back to
# EditorSession.openDocument would always enter the tab-creation pipeline.
open_case="$(sed -n '/case "open":/,/case "recoverUnsavedFiles":/p' "$MENU")"
printf '%s' "$open_case" | grep -Fq 'controller.openDocumentPanel()' \
    || fail "File Open must use the window-level open panel"

# Current-tab mode must replace the active session.  An already-open file must
# still activate its existing tab instead of duplicating it in the active tab.
require "$WINDOW" 'func openFileWithPreferredTarget' \
    'window must expose the preferred-target opener'
preferred_body="$(sed -n '/func openFileWithPreferredTarget/,/func openFileInTab/p' "$WINDOW")"
printf '%s' "$preferred_body" | grep -Fq 'settings.externalFileOpenMode' \
    || fail "File Open must check the current-tab preference"
printf '%s' "$preferred_body" | grep -Fq 'openDocumentBypassingRouter(at: url)' \
    || fail "File Open in current-tab mode must replace the active tab"
printf '%s' "$preferred_body" | grep -Fq 'activateTab(existing.tabID, animated: true)' \
    || fail "File Open must activate an already-open file"

echo "PASS"
