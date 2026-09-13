#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")/../.." && pwd)"
POLICY="$ROOT_DIR/Sources/MarkLeaf/Services/IncomingFileRoutingPolicy.swift"
ROUTER="$ROOT_DIR/Sources/MarkLeaf/Services/IncomingFileRouter.swift"
SETTINGS="$ROOT_DIR/Sources/MarkLeaf/Services/AppSettings.swift"
MODE="$ROOT_DIR/Sources/MarkLeaf/Services/ExternalFileOpenMode.swift"
MANAGER="$ROOT_DIR/Sources/MarkLeaf/App/AppWindowManager.swift"
SESSION="$ROOT_DIR/Sources/MarkLeaf/Services/EditorSession.swift"

require() {
  local file="$1" text="$2" message="$3"
  if ! grep -Fq "$text" "$file"; then
    echo "FAIL: $message" >&2
    exit 1
  fi
}

fail() {
    echo "FAIL: $1" >&2
    exit 1
}

# 外部文件模式必须包含"当前窗口新标签页"，否则多标签下语义含糊。
require "$MODE" 'case newTab' 'external file modes must include opening in a new tab'
require "$SETTINGS" '在当前窗口的新标签页中打开' 'the new-tab mode must be user visible'
require "$SETTINGS" '在当前标签页中打开' 'replace-active mode must say which tab it touches'

# 路由必须支持"活动窗口新标签页"这一动作。
require "$POLICY" 'case newTabInActiveWindow' 'routing must expose an active-window new-tab action'
require "$POLICY" 'case .newTab:' 'policy must route the new-tab mode explicitly'
require "$ROUTER" 'newTabInActiveWindow: (URL) -> Void' 'router must let the caller open a new tab in the active window'

# AppWindowManager 接线：去重覆盖所有窗口的所有标签，激活走 activateTab，新标签走 requestOpenFile。
require "$MANAGER" 'newTabInActiveWindow: { [weak self] url in' 'window manager must wire the new-tab action'
require "$MANAGER" 'windowSession?.requestOpenFile(url)' 'new-tab action must reuse the window tab pipeline'
require "$MANAGER" 'controller.activateTab(tab.tabID, animated: true)' 'duplicate activation must select the existing tab'
require "$MANAGER" 'tabStore.tabs' 'duplicate detection must cover background tabs too'

# “在当前标签页中打开”必须真正替换当前标签；不能再次进入 requestOpenFile 的建标签流程。
replace_active_body="$(sed -n '/replaceActive: { \[weak self\] url in/,/newTabInActiveWindow:/p' "$MANAGER")"
require_text_in_replace_active() {
    local text="$1" message="$2"
    if ! printf '%s' "$replace_active_body" | grep -Fq "$text"; then
        echo "FAIL: $message" >&2
        exit 1
    fi
}
require_text_in_replace_active 'activeTabSession?.openDocumentBypassingRouter(at: url)' \
    'current-tab action must bypass the tab-creation router'

# File → Recent Files is another file-entry path.  It must honor the
# “current tab” preference instead of always going through tab creation.
recent_file_body="$(sed -n '/func openRecentFile/,/func openRecentFolder/p' "$SESSION")"
printf '%s' "$recent_file_body" | grep -Fq 'externalFileOpenMode == .currentWindow' \
    || fail "recent files must check the current-tab preference"
printf '%s' "$recent_file_body" | grep -Fq 'openDocumentBypassingRouter(at: url)' \
    || fail "recent files in current-tab mode must replace the active tab"

echo "PASS"
