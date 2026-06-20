#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
UNITY_BIN="${UNITY_BIN:-/Users/kittenhao/Unity/UnityEditor/2022.3.34f1c1/Unity.app/Contents/MacOS/Unity}"
OUTPUT_PATH="${ANDROID_APK_PATH:-$ROOT_DIR/Builds/Android/MoonlyApp.apk}"
LOG_FILE="${ANDROID_BUILD_LOG:-$ROOT_DIR/Logs/android-apk-build.log}"

cd "$ROOT_DIR"

if [[ ! -x "$UNITY_BIN" ]]; then
  echo "Unity executable not found: $UNITY_BIN" >&2
  exit 127
fi

if [[ -f Temp/UnityLockfile ]] && command -v lsof >/dev/null 2>&1 && lsof Temp/UnityLockfile >/dev/null 2>&1; then
  echo "Unity currently has this project open. Close Unity before running batchmode Android build." >&2
  lsof Temp/UnityLockfile >&2 || true
  exit 2
fi

mkdir -p "$(dirname "$LOG_FILE")"
mkdir -p "$(dirname "$OUTPUT_PATH")"

if [[ "${CLEAN_ANDROID_BUILD:-0}" == "1" ]]; then
  rm -f "$OUTPUT_PATH"
fi

if ! "$UNITY_BIN" \
  -batchmode \
  -quit \
  -projectPath "$ROOT_DIR" \
  -executeMethod CommandLineBuild.BuildAndroidApk \
  -outputPath "$OUTPUT_PATH" \
  -logFile "$LOG_FILE"; then
  echo "Unity Android build failed. Log: $LOG_FILE" >&2
  tail -n 120 "$LOG_FILE" >&2 || true
  exit 1
fi

"$ROOT_DIR/scripts/check-android-config.sh" --apk "$OUTPUT_PATH"

echo "Android APK build finished and validated: $OUTPUT_PATH"
echo "Unity log: $LOG_FILE"
