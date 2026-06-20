#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT_ID="${FIREBASE_PROJECT:-fari-app-b2fd2}"
APK_PATH=""
FAILURES=0
WARNINGS=0

ok() {
  echo "[OK] $*"
}

warn() {
  echo "[WARN] $*"
  WARNINGS=$((WARNINGS + 1))
}

fail() {
  echo "[FAIL] $*"
  FAILURES=$((FAILURES + 1))
}

usage() {
  cat <<'EOF'
Usage:
  scripts/check-android-config.sh [--apk Builds/Android/MoonlyApp.apk]

Checks Android source configuration by default. With --apk, also checks the built APK manifest.
EOF
}

while [[ "$#" -gt 0 ]]; do
  case "$1" in
    --apk)
      APK_PATH="${2:-}"
      shift 2
      ;;
    --help|-h)
      usage
      exit 0
      ;;
    *)
      echo "Unknown argument: $1" >&2
      usage >&2
      exit 2
      ;;
  esac
done

find_aapt() {
  if command -v aapt >/dev/null 2>&1; then
    command -v aapt
    return 0
  fi

  find "$HOME/Library/Android/sdk" -type f -name aapt 2>/dev/null | sort -Vr | head -n 1
}

cd "$ROOT_DIR"

echo "Moonly Android config check"
echo "Project: $ROOT_DIR"
echo

SOURCE_RESULT_FILE="$(mktemp)"
trap 'rm -f "$SOURCE_RESULT_FILE"' EXIT

if python3 - "$ROOT_DIR" "$PROJECT_ID" <<'PY' | tee "$SOURCE_RESULT_FILE"
import os
import re
import sys
import xml.etree.ElementTree as ET

root, project_id = sys.argv[1], sys.argv[2]
failures = 0
warnings = 0
ANDROID_NS = "{http://schemas.android.com/apk/res/android}"

def ok(message):
    print(f"[OK] {message}")

def warn(message):
    global warnings
    warnings += 1
    print(f"[WARN] {message}")

def fail(message):
    global failures
    failures += 1
    print(f"[FAIL] {message}")

def read_text(path):
    with open(os.path.join(root, path), "r", encoding="utf-8-sig") as fh:
        return fh.read()

def parse_xml(path):
    try:
        return ET.parse(os.path.join(root, path)).getroot()
    except Exception as exc:
        fail(f"{path} is not valid XML: {exc}")
        return None

main_manifest_path = "Assets/Plugins/Android/AndroidManifest.xml"
main_manifest = parse_xml(main_manifest_path)
if main_manifest is not None:
    permissions = {
        node.attrib.get(ANDROID_NS + "name", "")
        for node in main_manifest.findall("uses-permission")
    }
    for permission in ("android.permission.READ_CONTACTS", "android.permission.POST_NOTIFICATIONS"):
        if permission in permissions:
            ok(f"AndroidManifest declares {permission}")
        else:
            fail(f"AndroidManifest missing {permission}")

    application = main_manifest.find("application")
    debuggable = application.attrib.get(ANDROID_NS + "debuggable", "") if application is not None else ""
    if debuggable.lower() == "true":
        fail("AndroidManifest application is debuggable=true")
    else:
        ok("AndroidManifest application is not debuggable")

    metadata = {
        node.attrib.get(ANDROID_NS + "name", ""): node.attrib.get(ANDROID_NS + "value", "")
        for node in application.findall("meta-data")
    } if application is not None else {}
    if metadata.get("com.facebook.sdk.ApplicationId", "").startswith("fb"):
        ok("Facebook ApplicationId is configured in AndroidManifest")
    else:
        fail("Facebook ApplicationId missing in AndroidManifest")
    if metadata.get("com.facebook.sdk.ClientToken", ""):
        ok("Facebook ClientToken is configured in AndroidManifest")
    else:
        fail("Facebook ClientToken missing in AndroidManifest")

google_manifest = parse_xml("Assets/Plugins/Android/FariGoogleSignIn.androidlib/AndroidManifest.xml")
if google_manifest is not None:
    activity = google_manifest.find("./application/activity")
    exported = activity.attrib.get(ANDROID_NS + "exported", "") if activity is not None else ""
    if exported.lower() == "false":
        ok("Google Sign-In bridge activity is not exported")
    else:
        fail("Google Sign-In bridge activity should be android:exported=false")

services = parse_xml("Assets/Plugins/Android/FirebaseApp.androidlib/res/values/google-services.xml")
if services is not None:
    values = {
        node.attrib.get("name", ""): (node.text or "")
        for node in services.findall("string")
    }
    if values.get("project_id") == project_id:
        ok(f"Firebase google-services project_id is {project_id}")
    else:
        fail(f"Firebase google-services project_id is not {project_id}")
    for key in ("google_app_id", "google_api_key", "default_android_client_id", "default_web_client_id"):
        if values.get(key):
            ok(f"Firebase google-services has {key}")
        else:
            fail(f"Firebase google-services missing {key}")

play_games = parse_xml("Assets/Plugins/Android/GooglePlayGamesManifest.androidlib/AndroidManifest.xml")
if play_games is not None:
    application = play_games.find("application")
    metadata = {
        node.attrib.get(ANDROID_NS + "name", ""): node.attrib.get(ANDROID_NS + "value", "")
        for node in application.findall("meta-data")
    } if application is not None else {}
    app_id = metadata.get("com.google.android.gms.games.APP_ID", "")
    normalized_app_id = app_id.replace("\\u003", "", 1)
    if normalized_app_id and normalized_app_id.isdigit() and len(normalized_app_id) > 3:
        ok("Google Play Games APP_ID is configured")
    else:
        warn("Google Play Games APP_ID appears to be a placeholder; configure it with scripts/configure-google-play-games.sh before using Play Games services")

settings = read_text("ProjectSettings/ProjectSettings.asset")
android_bundle = re.search(r"applicationIdentifier:\s*\n(?:.*\n)*?\s+Android:\s*([^\s]+)", settings)
bundle_id = android_bundle.group(1) if android_bundle else ""
if bundle_id:
    ok(f"ProjectSettings Android bundle id is {bundle_id}")
else:
    fail("ProjectSettings Android bundle id missing")

def int_field(name):
    match = re.search(rf"{re.escape(name)}:\s*([0-9]+)", settings)
    return int(match.group(1)) if match else None

min_sdk = int_field("AndroidMinSdkVersion")
target_sdk = int_field("AndroidTargetSdkVersion")
version_code = int_field("AndroidBundleVersionCode")
if min_sdk is not None and min_sdk >= 23:
    ok(f"Android minSdkVersion is {min_sdk}")
else:
    fail("Android minSdkVersion should be >= 23")
if target_sdk is not None and target_sdk >= 33:
    ok(f"Android targetSdkVersion is {target_sdk}")
else:
    fail("Android targetSdkVersion should be >= 33 for notification permission")
if version_code is not None and version_code >= 1:
    ok(f"Android bundle version code is {version_code}")
else:
    fail("Android bundle version code missing or invalid")

defines = re.search(r"scriptingDefineSymbols:\s*\n(?:.*\n)*?\s+Android:\s*(.*)", settings)
if defines and "UNITY_PURCHASING" in defines.group(1):
    ok("ProjectSettings Android scripting symbols include UNITY_PURCHASING")
else:
    fail("ProjectSettings Android scripting symbols missing UNITY_PURCHASING")

main_gradle = read_text("Assets/Plugins/Android/mainTemplate.gradle")
for dependency in ("com.google.firebase:firebase-auth", "com.google.firebase:firebase-firestore", "com.facebook.android:facebook-login", "com.google.android.gms:play-services-base"):
    if dependency in main_gradle:
        ok(f"mainTemplate.gradle has {dependency}")
    else:
        fail(f"mainTemplate.gradle missing {dependency}")

settings_gradle = read_text("Assets/Plugins/Android/settingsTemplate.gradle")
for repo in ("google()", "mavenCentral()"):
    if repo in settings_gradle:
        ok(f"settingsTemplate.gradle has {repo}")
    else:
        fail(f"settingsTemplate.gradle missing {repo}")

print(f"PY_RESULT failures={failures} warnings={warnings}")
sys.exit(1 if failures else 0)
PY
then
  :
else
  FAILURES=$((FAILURES + 1))
fi

source_warnings="$(sed -n 's/^PY_RESULT failures=[0-9][0-9]* warnings=\([0-9][0-9]*\)$/\1/p' "$SOURCE_RESULT_FILE" | tail -n 1)"
if [[ -n "$source_warnings" ]]; then
  WARNINGS=$((WARNINGS + source_warnings))
fi

if [[ -n "$APK_PATH" ]]; then
  if [[ ! -f "$APK_PATH" ]]; then
    fail "APK not found: $APK_PATH"
  else
    AAPT="$(find_aapt)"
    if [[ -z "$AAPT" || ! -x "$AAPT" ]]; then
      fail "aapt not found; cannot inspect APK"
    else
      badging="$("$AAPT" dump badging "$APK_PATH")"
      permissions="$("$AAPT" dump permissions "$APK_PATH")"
      xmltree="$("$AAPT" dump xmltree "$APK_PATH" AndroidManifest.xml)"

      expected_package="$(python3 - "$ROOT_DIR/ProjectSettings/ProjectSettings.asset" <<'PY'
import re
import sys
text = open(sys.argv[1], encoding="utf-8-sig").read()
match = re.search(r"applicationIdentifier:\s*\n(?:.*\n)*?\s+Android:\s*([^\s]+)", text)
print(match.group(1) if match else "")
PY
)"

      if grep -q "package: name='$expected_package'" <<<"$badging"; then
        ok "APK package id is $expected_package"
      else
        fail "APK package id does not match $expected_package"
      fi

      for permission in android.permission.POST_NOTIFICATIONS android.permission.READ_CONTACTS; do
        if grep -q "uses-permission: name='$permission'" <<<"$permissions"; then
          ok "APK declares $permission"
        else
          fail "APK missing $permission"
        fi
      done

      if grep -q "android:debuggable.*0xffffffff" <<<"$xmltree"; then
        fail "APK manifest is debuggable"
      else
        ok "APK manifest is not debuggable"
      fi
    fi
  fi
fi

echo
echo "Summary: $FAILURES failure(s), $WARNINGS warning(s)"
if [[ "$FAILURES" -gt 0 ]]; then
  exit 1
fi

exit 0
