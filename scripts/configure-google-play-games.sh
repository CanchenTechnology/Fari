#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
MANIFEST_PATH="$ROOT_DIR/Assets/Plugins/Android/GooglePlayGamesManifest.androidlib/AndroidManifest.xml"
APP_ID="${1:-${GOOGLE_PLAY_GAMES_APP_ID:-}}"

usage() {
  cat <<'EOF'
Usage:
  GOOGLE_PLAY_GAMES_APP_ID=123456789012 scripts/configure-google-play-games.sh
  scripts/configure-google-play-games.sh 123456789012

Updates Assets/Plugins/Android/GooglePlayGamesManifest.androidlib/AndroidManifest.xml.
The value is written as \u003<APP_ID>, matching the Google Play Games Unity plugin string format.
EOF
}

if [[ "${1:-}" == "--help" || "${1:-}" == "-h" ]]; then
  usage
  exit 0
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

if [[ ! -f "$MANIFEST_PATH" ]]; then
  echo "Google Play Games manifest not found: $MANIFEST_PATH" >&2
  exit 1
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
