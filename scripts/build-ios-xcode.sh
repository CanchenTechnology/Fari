#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RELEASE_ENV_FILE="${RELEASE_ENV_FILE:-}"

if [[ -n "$RELEASE_ENV_FILE" ]]; then
  if [[ "$RELEASE_ENV_FILE" != /* ]]; then
    RELEASE_ENV_FILE="$ROOT_DIR/$RELEASE_ENV_FILE"
  fi

  if [[ ! -f "$RELEASE_ENV_FILE" ]]; then
    echo "Release env file does not exist: $RELEASE_ENV_FILE" >&2
    exit 3
  fi

  set -a
  # shellcheck disable=SC1090
  . "$RELEASE_ENV_FILE"
  set +a
  export MOONLY_RELEASE_ENV_FILE="$RELEASE_ENV_FILE"
fi

UNITY_BIN="${UNITY_BIN:-/Users/kittenhao/Unity/UnityEditor/2022.3.34f1c1/Unity.app/Contents/MacOS/Unity}"
OUTPUT_PATH="${IOS_EXPORT_PATH:-$ROOT_DIR/Builds/iOS}"
LOG_FILE="${IOS_BUILD_LOG:-$ROOT_DIR/Logs/ios-xcode-build.log}"

cd "$ROOT_DIR"

if [[ -n "${MOONLY_RELEASE_ENV_FILE:-}" ]]; then
  echo "Release env file: $MOONLY_RELEASE_ENV_FILE"
fi

if [[ ! -x "$UNITY_BIN" ]]; then
  echo "Unity executable not found: $UNITY_BIN" >&2
  exit 127
fi

if [[ -f Temp/UnityLockfile ]] && command -v lsof >/dev/null 2>&1 && lsof Temp/UnityLockfile >/dev/null 2>&1; then
  echo "Unity currently has this project open. Close Unity before running batchmode iOS export." >&2
  lsof Temp/UnityLockfile >&2 || true
  exit 2
fi

mkdir -p "$(dirname "$LOG_FILE")"
mkdir -p "$(dirname "$OUTPUT_PATH")"

if [[ "${CLEAN_IOS_EXPORT:-0}" == "1" ]]; then
  rm -rf "$OUTPUT_PATH"
fi

if ! "$UNITY_BIN" \
  -batchmode \
  -quit \
  -buildTarget iOS \
  -projectPath "$ROOT_DIR" \
  -executeMethod CommandLineBuild.BuildIOSProject \
  -outputPath "$OUTPUT_PATH" \
  -logFile "$LOG_FILE"; then
  echo "Unity iOS export failed. Log: $LOG_FILE" >&2
  tail -n 120 "$LOG_FILE" >&2 || true
  exit 1
fi

"$ROOT_DIR/scripts/check-ios-export.sh" "$OUTPUT_PATH"

echo "iOS Xcode export finished and validated: $OUTPUT_PATH"
echo "Unity log: $LOG_FILE"
