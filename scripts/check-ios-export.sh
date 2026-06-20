#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

ok() {
  echo "[OK] $*"
}

fail() {
  echo "[FAIL] $*" >&2
  exit 1
}

make_self_test_project() {
  local target="$1"
  mkdir -p "$target/Unity-iPhone.xcodeproj"

  python3 - "$target" <<'PY'
import os
import plistlib
import sys

root = sys.argv[1]
with open(os.path.join(root, "Info.plist"), "wb") as fh:
    plistlib.dump({
        "CFBundleIdentifier": "com.canchentechnology.fari",
        "NSContactsUsageDescription": "用于选择你想邀请加入 Moonly 的联系人。我们不会自动发送消息。",
    }, fh)

with open(os.path.join(root, "fari.entitlements"), "wb") as fh:
    plistlib.dump({
        "com.apple.developer.game-center": True,
        "com.apple.developer.applesignin": ["Default"],
    }, fh)
PY

  cat > "$target/Unity-iPhone.xcodeproj/project.pbxproj" <<'PBX'
/* Mock pbxproj for check-ios-export self-test. */
{
  CODE_SIGN_ENTITLEMENTS = fari.entitlements;
  SystemCapabilities = {
    com.apple.GameCenter = {
      enabled = 1;
    };
    com.apple.SignInWithApple = {
      enabled = 1;
    };
    com.apple.InAppPurchase = {
      enabled = 1;
    };
  };
}
PBX
}

resolve_export_path() {
  local input_path="$1"
  if [[ "$input_path" = /* ]]; then
    printf "%s\n" "$input_path"
  else
    printf "%s\n" "$ROOT_DIR/$input_path"
  fi
}

find_entitlements_file() {
  local export_path="$1"
  if [[ -f "$export_path/fari.entitlements" ]]; then
    printf "%s\n" "$export_path/fari.entitlements"
    return 0
  fi

  find "$export_path" -maxdepth 2 -type f -name "*.entitlements" | head -n 1
}

check_export() {
  local export_path="$1"
  export_path="$(resolve_export_path "$export_path")"
  [[ -d "$export_path" ]] || fail "iOS export directory not found: $export_path"

  local pbxproj="$export_path/Unity-iPhone.xcodeproj/project.pbxproj"
  local info_plist="$export_path/Info.plist"
  local entitlements
  entitlements="$(find_entitlements_file "$export_path")"

  [[ -f "$pbxproj" ]] || fail "missing Xcode project: $pbxproj"
  [[ -f "$info_plist" ]] || fail "missing Info.plist: $info_plist"
  [[ -n "$entitlements" && -f "$entitlements" ]] || fail "missing .entitlements file under $export_path"

  python3 - "$info_plist" "$entitlements" <<'PY'
import plistlib
import sys

info_path, entitlements_path = sys.argv[1], sys.argv[2]

with open(info_path, "rb") as fh:
    info = plistlib.load(fh)

contacts = info.get("NSContactsUsageDescription", "")
if not isinstance(contacts, str) or not contacts.strip():
    raise SystemExit("Info.plist missing NSContactsUsageDescription")

with open(entitlements_path, "rb") as fh:
    entitlements = plistlib.load(fh)

if entitlements.get("com.apple.developer.game-center") is not True:
    raise SystemExit("entitlements missing com.apple.developer.game-center=true")

apple_sign_in = entitlements.get("com.apple.developer.applesignin")
if not isinstance(apple_sign_in, list) or "Default" not in apple_sign_in:
    raise SystemExit("entitlements missing com.apple.developer.applesignin Default")
PY

  for needle in \
    "CODE_SIGN_ENTITLEMENTS" \
    "com.apple.GameCenter" \
    "com.apple.InAppPurchase"; do
    if ! grep -q "$needle" "$pbxproj"; then
      fail "project.pbxproj missing $needle"
    fi
  done

  if ! grep -q "com.apple.SignInWithApple" "$pbxproj" \
    && ! grep -q "com.apple.developer.applesignin" "$pbxproj"; then
    fail "project.pbxproj missing Sign in with Apple capability"
  fi

  ok "iOS export contains contacts usage, Game Center, Apple Sign-In, and In-App Purchase capabilities"
}

if [[ "${1:-}" == "--self-test" ]]; then
  tmp_dir="$(mktemp -d)"
  trap 'rm -rf "$tmp_dir"' EXIT
  make_self_test_project "$tmp_dir"
  check_export "$tmp_dir"
  exit 0
fi

check_export "${1:-Builds/iOS}"
