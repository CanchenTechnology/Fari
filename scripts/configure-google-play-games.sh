#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
MANIFEST_PATH="$ROOT_DIR/Assets/Plugins/Android/GooglePlayGamesManifest.androidlib/AndroidManifest.xml"
APP_ID="${1:-${GOOGLE_PLAY_GAMES_APP_ID:-}}"
CHECK_ONLY=0
DRY_RUN="${DRY_RUN:-0}"

usage() {
  cat <<'EOF'
Usage:
  GOOGLE_PLAY_GAMES_APP_ID=123456789012 scripts/configure-google-play-games.sh
  scripts/configure-google-play-games.sh 123456789012
  scripts/configure-google-play-games.sh --check
  DRY_RUN=1 GOOGLE_PLAY_GAMES_APP_ID=123456789012 scripts/configure-google-play-games.sh

Updates Assets/Plugins/Android/GooglePlayGamesManifest.androidlib/AndroidManifest.xml.
The value is written as \u003<APP_ID>, matching the Google Play Games Unity plugin string format.
--check only verifies that the manifest already contains a non-placeholder numeric App ID.
DRY_RUN=1 validates the provided value and prints what would be written without changing files.
EOF
}

while [[ "$#" -gt 0 ]]; do
  case "$1" in
    --help|-h)
      usage
      exit 0
      ;;
    --check)
      CHECK_ONLY=1
      shift
      ;;
    *)
      APP_ID="$1"
      shift
      ;;
  esac
done

read_manifest_app_id() {
  python3 - "$MANIFEST_PATH" <<'PY'
import sys
import xml.etree.ElementTree as ET

path = sys.argv[1]
ANDROID_NS = "{http://schemas.android.com/apk/res/android}"
root = ET.parse(path).getroot()
app = root.find("application")
metadata = {
    node.attrib.get(ANDROID_NS + "name", ""): node.attrib.get(ANDROID_NS + "value", "")
    for node in app.findall("meta-data")
} if app is not None else {}
print(metadata.get("com.google.android.gms.games.APP_ID", ""))
PY
}

if [[ ! -f "$MANIFEST_PATH" ]]; then
  echo "Google Play Games manifest not found: $MANIFEST_PATH" >&2
  exit 1
fi

if [[ "$CHECK_ONLY" == "1" ]]; then
  current_app_id="$(read_manifest_app_id)"
  normalized_app_id="${current_app_id#\\u003}"
  if [[ "$normalized_app_id" =~ ^[0-9]{4,}$ ]]; then
    echo "Google Play Games APP_ID is configured: $normalized_app_id"
    exit 0
  fi

  echo "Google Play Games APP_ID is missing or placeholder: ${current_app_id:-<missing>}" >&2
  exit 1
fi

if [[ -z "$APP_ID" ]]; then
  echo "GOOGLE_PLAY_GAMES_APP_ID is required." >&2
  usage >&2
  exit 2
fi

if [[ ! "$APP_ID" =~ ^[0-9]{4,}$ ]]; then
  echo "GOOGLE_PLAY_GAMES_APP_ID must be numeric and at least 4 digits: $APP_ID" >&2
  exit 2
fi

if [[ "$DRY_RUN" == "1" ]]; then
  echo "Dry run OK. Google Play Games APP_ID would be written as: \\u003$APP_ID"
  echo "Target: $MANIFEST_PATH"
  exit 0
fi

python3 - "$MANIFEST_PATH" "$APP_ID" <<'PY'
import re
import sys

path, app_id = sys.argv[1], sys.argv[2]
with open(path, "r", encoding="utf-8-sig") as fh:
    text = fh.read()

pattern = re.compile(
    r'(<meta-data\s+android:name="com\.google\.android\.gms\.games\.APP_ID"\s+android:value=")[^"]*(")',
    re.MULTILINE,
)

replacement = rf"\1\\u003{app_id}\2"
new_text, count = pattern.subn(replacement, text, count=1)
if count != 1:
    raise SystemExit("Could not find com.google.android.gms.games.APP_ID meta-data entry.")

with open(path, "w", encoding="utf-8", newline="\n") as fh:
    fh.write(new_text)
PY

echo "Google Play Games APP_ID configured: $APP_ID"
echo "Next check:"
echo "  ./scripts/check-android-config.sh"
