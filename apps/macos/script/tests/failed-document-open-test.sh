#!/usr/bin/env bash
set -euo pipefail
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
BUILD_DIR="$(mktemp -d "${TMPDIR:-/tmp}/markleaf-failed-document-open-test.XXXXXX")"
trap 'rm -rf "$BUILD_DIR"' EXIT
if [[ -z "${DEVELOPER_DIR:-}" && -d /Applications/Xcode.app/Contents/Developer ]]; then
  export DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer
fi
swift build --package-path "$ROOT_DIR" --build-tests
BIN_DIR="$(swift build --package-path "$ROOT_DIR" --show-bin-path)"
OBJECTS=()
if [[ -d "$BIN_DIR/Modules" ]]; then
  MODULE_DIR="$BIN_DIR/Modules"
  for object in "$BIN_DIR/MarkLeaf.build/"*.swift.o; do
    [[ "$object" == */main.swift.o ]] || OBJECTS+=("$object")
  done
else
  MODULE_FILE="$(find "$ROOT_DIR/.build/out/Intermediates.noindex/MarkLeaf.build" -path '*MarkLeaf-p.build/Objects-normal/*/MarkLeaf.swiftmodule' -print | head -1)"
  [[ -n "$MODULE_FILE" ]] || { echo "FAIL: MarkLeaf module was not produced" >&2; exit 1; }
  MODULE_DIR="$(dirname "$MODULE_FILE")"
  LINK_FILE="$(find "$ROOT_DIR/.build/out/Intermediates.noindex/MarkLeaf.build" -path '*MarkLeaf-p.build/Objects-normal/*/MarkLeaf.LinkFileList' -print | head -1)"
  [[ -n "$LINK_FILE" ]] || { echo "FAIL: MarkLeaf link file list was not produced" >&2; exit 1; }
  while IFS= read -r object; do
    object="${object#\"}"; object="${object%\"}"
    [[ "$object" == */main.o ]] || OBJECTS+=("$object")
  done < <(tr ' ' '\n' < "$LINK_FILE")
fi
[[ ${#OBJECTS[@]} -gt 0 ]] || { echo "FAIL: MarkLeaf object files were not found" >&2; exit 1; }
xcrun swiftc -I "$MODULE_DIR" -module-cache-path "$BUILD_DIR/module-cache" \
  "$ROOT_DIR/script/tests/FailedDocumentOpenTest.swift" "${OBJECTS[@]}" -o "$BUILD_DIR/test"
MARKLEAF_APP_SUPPORT_DIR="$BUILD_DIR/settings" "$BUILD_DIR/test"
