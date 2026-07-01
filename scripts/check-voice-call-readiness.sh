#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

failures=0

run_check() {
  local label="$1"
  shift

  echo
  echo "== $label =="
  set +e
  "$@"
  local status=$?
  set -e

  if [[ "$status" -eq 0 ]]; then
    echo "[OK] $label"
  else
    echo "[FAIL] $label" >&2
    failures=$((failures + 1))
  fi
}

if [[ "${CHECK_VOICE_CALL_BUILD:-0}" == "1" ]]; then
  run_check "Unity C# compile" dotnet build Assembly-CSharp.csproj --no-restore
fi

run_check "AI and TTS authenticated smoke" \
  env SMOKE_CONTINUE_ON_FAILURE=1 REQUIRE_AI_TTS_LIVE=1 ./scripts/smoke-functions-auth.sh

run_check "Voice ASR authenticated smoke" \
  env VOICE_ASR_REQUIRE_LIVE=1 ./scripts/smoke-voice-asr.sh

echo
if [[ "$failures" -gt 0 ]]; then
  echo "Voice call readiness: $failures check(s) failed" >&2
  echo "Required for live calls: AI smoke, TTS smoke, and ASR smoke must all pass." >&2
  exit 1
fi

echo "Voice call readiness: all checks passed"
