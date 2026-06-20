#!/usr/bin/env bash
set -euo pipefail

PROJECT_ID="${FIREBASE_PROJECT:-fari-app-b2fd2}"
READINESS_URL="${READINESS_URL:-https://us-central1-$PROJECT_ID.cloudfunctions.net/readinessStatus}"
CONNECT_TIMEOUT="${CONNECT_TIMEOUT:-8}"
MAX_TIME="${MAX_TIME:-18}"
PROBE_RETRIES="${PROBE_RETRIES:-2}"
FAILURES=0
WARNINGS=0

if [[ -n "${MOONLY_PROXY:-}" ]]; then
  export HTTPS_PROXY="$MOONLY_PROXY"
  export HTTP_PROXY="$MOONLY_PROXY"
fi

if [[ -n "${MOONLY_ALL_PROXY:-}" ]]; then
  export ALL_PROXY="$MOONLY_ALL_PROXY"
fi

if ! command -v curl >/dev/null 2>&1; then
  echo "[FAIL] curl not found"
  exit 127
fi

TMP_DIR="$(mktemp -d)"
trap 'rm -rf "$TMP_DIR"' EXIT

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

probe() {
  local name="$1"
  local url="$2"
  local mode="${3:-connectivity}"
  local body_file="$TMP_DIR/${name//[^A-Za-z0-9_]/_}.body"
  local err_file="$TMP_DIR/${name//[^A-Za-z0-9_]/_}.err"
  local code=""
  local err=""
  local attempt=0
  local max_attempts=$((PROBE_RETRIES + 1))

  while [[ "$attempt" -lt "$max_attempts" ]]; do
    attempt=$((attempt + 1))
    code="$(
      curl \
        --silent \
        --show-error \
        --location \
        --connect-timeout "$CONNECT_TIMEOUT" \
        --max-time "$MAX_TIME" \
        --output "$body_file" \
        --write-out "%{http_code}" \
        "$url" \
        2>"$err_file"
    )" && [[ "$code" != "000" ]] && break

    err="$(head -n 1 "$err_file" 2>/dev/null || true)"
    if [[ "$attempt" -lt "$max_attempts" ]]; then
      sleep "$attempt"
    fi
  done

  if [[ "$code" == "000" ]]; then
    fail "$name unreachable after $max_attempts attempt(s): no HTTP response${err:+; $err}"
    return
  fi

  if [[ -z "$code" ]]; then
    fail "$name unreachable after $max_attempts attempt(s)${err:+: $err}"
    return
  fi

  case "$mode" in
    readiness)
      if [[ "$code" == "200" ]]; then
        ok "$name reachable and deployed (HTTP $code)"
      else
        warn "$name reachable but not ready/deployed yet (HTTP $code)"
      fi
      ;;
    *)
      if [[ "$code" =~ ^[234] ]]; then
        ok "$name reachable (HTTP $code)"
      elif [[ "$code" =~ ^5 ]]; then
        warn "$name reached server but returned HTTP $code"
      else
        ok "$name reachable (HTTP $code)"
      fi
      ;;
  esac
}

echo "Moonly Firebase network check"
echo "Project: $PROJECT_ID"
if [[ -n "${HTTPS_PROXY:-}" || -n "${ALL_PROXY:-}" ]]; then
  echo "Proxy: HTTPS_PROXY=${HTTPS_PROXY:-} ALL_PROXY=${ALL_PROXY:-}"
else
  echo "Proxy: none"
fi
echo

probe "Firestore REST" "https://firestore.googleapis.com/v1/projects/$PROJECT_ID/databases/(default)/documents"
probe "Firebase Auth REST" "https://identitytoolkit.googleapis.com/v1/projects/$PROJECT_ID/config?key=missing-api-key"
probe "Secure Token REST" "https://securetoken.googleapis.com/v1/token?key=missing-api-key"
probe "Firebase Management" "https://firebase.googleapis.com/v1beta1/projects/$PROJECT_ID"
probe "readinessStatus" "$READINESS_URL" "readiness"

echo
echo "Summary: $FAILURES failure(s), $WARNINGS warning(s)"
if [[ "$FAILURES" -gt 0 ]]; then
  echo "Network check failed. If Unity Editor also reports Firestore Unavailable, try MOONLY_PROXY / MOONLY_ALL_PROXY with a reachable local proxy." >&2
  exit 1
fi

exit 0
