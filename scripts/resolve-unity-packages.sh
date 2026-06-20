#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
UNITY_BIN="${UNITY_BIN:-/Users/kittenhao/Unity/UnityEditor/2022.3.34f1c1/Unity.app/Contents/MacOS/Unity}"
LOG_FILE="${UNITY_PACKAGE_RESOLVE_LOG:-$ROOT_DIR/Logs/package-resolve-batch.log}"

cd "$ROOT_DIR"

if [[ ! -x "$UNITY_BIN" ]]; then
  echo "Unity executable not found: $UNITY_BIN" >&2
  exit 127
fi

if [[ -f Temp/UnityLockfile ]] && command -v lsof >/dev/null 2>&1 && lsof Temp/UnityLockfile >/dev/null 2>&1; then
  echo "Unity currently has this project open. Close Unity or use Tools/Moonly/Resolve Required Packages inside the open Editor." >&2
  lsof Temp/UnityLockfile >&2 || true
  exit 2
fi

mkdir -p "$(dirname "$LOG_FILE")"

"$UNITY_BIN" \
  -batchmode \
  -projectPath "$ROOT_DIR" \
  -executeMethod AppPackageResolverMenu.ResolveRequiredPackagesBatchMode \
  -logFile "$LOG_FILE"

echo "Unity package resolve finished. Log: $LOG_FILE"
CHECK_REGISTRY=1 "$ROOT_DIR/scripts/check-local-readiness.sh"
