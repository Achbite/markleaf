#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")/../.." && pwd)"
MENU="$ROOT_DIR/Sources/MarkLeaf/Support/NativeMenuBuilder.swift"
TAB_BAR="$ROOT_DIR/Sources/MarkLeaf/Views/TabBarController.swift"

fail() {
    echo "FAIL: $1" >&2
    exit 1
}

# Save commands must be unavailable when the document has no unsaved changes.
grep -Fq 'SaveMenuPolicy.isSaveEnabled' "$MENU" \
    || fail "Save must use the dirty-state policy"
grep -Fq 'SaveMenuPolicy.isSaveAllEnabled' "$MENU" \
    || fail "Save All must use the dirty-state policy"

# File → Rename must share the sidebar's workspace rename semantics.
rename_menu_body="$(sed -n '/case "renameActiveTab":/,/case "recoverUnsavedFiles":/p' "$MENU")"
printf '%s' "$rename_menu_body" | grep -Fq 'renameActiveTab' \
    || fail "File menu must expose Rename and route it explicitly"
printf '%s' "$rename_menu_body" | grep -Fq 'activeTabSession?.documentURL != nil' \
    || fail "Rename must be available for any active local document"
printf '%s' "$rename_menu_body" | grep -Fq 'workspaceRoot' \
    && fail "Rename must not require the document to live inside a workspace" || true

# Tab context menus own tab lifecycle only; file actions remain in the File menu.
context_body="$(sed -n '/private func showContextMenu/,/NSMenu.popUpContextMenu/p' "$TAB_BAR")"
printf '%s' "$context_body" | grep -Fq '在工作区定位' \
    && fail "tab context menu must not contain Reveal in Workspace" || true
printf '%s' "$context_body" | grep -Fq '复制文件路径' \
    && fail "tab context menu must not contain Copy File Path" || true
printf '%s' "$context_body" | grep -Fq '复制内容到剪贴板' \
    && fail "tab context menu must not contain Copy Content to Clipboard" || true
printf '%s' "$context_body" | grep -Fq '在 Finder 中显示' \
    && fail "tab context menu must not contain Reveal in Finder" || true
printf '%s' "$context_body" | grep -Fq '"分享"' \
    && fail "tab context menu must not contain Share" || true

echo "PASS"
