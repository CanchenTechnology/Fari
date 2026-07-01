#!/usr/bin/env bash
set -euo pipefail

PROJECT_ID="${FIREBASE_PROJECT:-fari-app-b2fd2}"
READINESS_URL="${READINESS_URL:-https://us-central1-$PROJECT_ID.cloudfunctions.net/readinessStatus}"
TMP_DIR="$(mktemp -d)"
READINESS_JSON="$TMP_DIR/readiness.json"
READINESS_STATE="unfetched"

REQUIRED_SECRETS=(
  DASHSCOPE_API_KEY
  VOLC_TTS_API_KEY
  APPLE_SHARED_SECRET
  GOOGLE_PACKAGE_NAME
  GOOGLE_SERVICE_ACCOUNT_JSON
)

OPTIONAL_SECRETS=(
  VOICE_APP_ID
  VOICE_ACCESS_KEY
  PAYMENT_WEBHOOK_SECRET
)

if [[ -n "${MOONLY_PROXY:-}" ]]; then
  export HTTPS_PROXY="$MOONLY_PROXY"
  export HTTP_PROXY="$MOONLY_PROXY"
fi

if [[ -n "${MOONLY_ALL_PROXY:-}" ]]; then
  export ALL_PROXY="$MOONLY_ALL_PROXY"
fi

cleanup() {
  rm -rf "$TMP_DIR"
}
trap cleanup EXIT

ok() {
  echo "[OK] $*"
}

warn() {
  echo "[WARN] $*"
}

fail() {
  echo "[FAIL] $*"
}

require_command() {
  local command_name="$1"
  if ! command -v "$command_name" >/dev/null 2>&1; then
    fail "$command_name not found"
    exit 127
  fi
}

require_command node

run_with_timeout() {
  local seconds="$1"
  shift

  "$@" &
  local command_pid=$!
  (
    sleep "$seconds"
    kill "$command_pid" >/dev/null 2>&1 || true
  ) &
  local watchdog_pid=$!

  set +e
  wait "$command_pid"
  local status=$?
  set -e

  kill "$watchdog_pid" >/dev/null 2>&1 || true
  wait "$watchdog_pid" >/dev/null 2>&1 || true

  return "$status"
}

fetch_readiness_status() {
  if [[ "$READINESS_STATE" != "unfetched" ]]; then
    return
  fi

  READINESS_STATE="unavailable"
  if ! command -v curl >/dev/null 2>&1; then
    return
  fi

  local readiness_timeout="${READINESS_TIMEOUT_SECONDS:-30}"
  local readiness_connect_timeout="${READINESS_CONNECT_TIMEOUT_SECONDS:-10}"
  if ! curl \
    -fsS \
    --connect-timeout "$readiness_connect_timeout" \
    --max-time "$readiness_timeout" \
    "$READINESS_URL" \
    >"$READINESS_JSON" \
    2>"$TMP_DIR/readiness.err"; then
    return
  fi

  if node -e 'const fs=require("fs"); const data=JSON.parse(fs.readFileSync(process.argv[1],"utf8")); process.exit(data && data.secrets ? 0 : 1);' "$READINESS_JSON" >/dev/null 2>&1; then
    READINESS_STATE="available"
  fi
}

readiness_secret_state() {
  local key="$1"
  fetch_readiness_status

  if [[ "$READINESS_STATE" != "available" ]]; then
    echo "unknown"
    return
  fi

  node - "$key" "$READINESS_JSON" <<'NODE'
const fs = require("fs");
const key = process.argv[2];
const file = process.argv[3];
const data = JSON.parse(fs.readFileSync(file, "utf8"));
const map = {
  DASHSCOPE_API_KEY: "dashscopeApiKey",
  VOLC_TTS_API_KEY: "volcanoTtsApiKey",
  VOICE_APP_ID: "voiceAppId",
  VOICE_ACCESS_KEY: "voiceAccessKey",
  APPLE_SHARED_SECRET: "appleSharedSecret",
  GOOGLE_PACKAGE_NAME: "googlePackageName",
  GOOGLE_SERVICE_ACCOUNT_JSON: "googleServiceAccountJson",
  PAYMENT_WEBHOOK_SECRET: "paymentWebhookSecret",
};

const field = map[key];
const value = field && data.secrets ? data.secrets[field] : undefined;
if (value === true) {
  process.stdout.write("present");
} else if (value === false) {
  process.stdout.write("missing");
} else {
  process.stdout.write("unknown");
}
NODE
}

ensure_firebase_cli_ready() {
  require_command firebase

  if ! firebase login:list --json >"$TMP_DIR/firebase-login.json" 2>"$TMP_DIR/firebase-login.err"; then
    fail "Firebase CLI is not logged in; run firebase login --reauth"
    return 1
  fi

  if ! node - "$TMP_DIR/firebase-login.json" <<'NODE' >/dev/null 2>&1
const fs = require("fs");
const data = JSON.parse(fs.readFileSync(process.argv[2], "utf8"));
process.exit(Array.isArray(data.result) && data.result.length > 0 ? 0 : 1);
NODE
  then
    fail "Firebase CLI has no active login; run firebase login --reauth"
    return 1
  fi

  if ! firebase use --json >"$TMP_DIR/firebase-use.json" 2>"$TMP_DIR/firebase-use.err"; then
    fail "Firebase project is not selected; run firebase use $PROJECT_ID"
    return 1
  fi

  if ! node - "$PROJECT_ID" "$TMP_DIR/firebase-use.json" <<'NODE' >/dev/null 2>&1
const fs = require("fs");
const expectedProject = process.argv[2];
const data = JSON.parse(fs.readFileSync(process.argv[3], "utf8"));
process.exit(data.result === expectedProject ? 0 : 1);
NODE
  then
    fail "Firebase active project is not $PROJECT_ID; run firebase use $PROJECT_ID"
    return 1
  fi
}

check_secret() {
  local key="$1"
  local required="$2"
  local output="$TMP_DIR/$key.value"
  local stderr="$TMP_DIR/$key.err"
  local readiness_state

  readiness_state="$(readiness_secret_state "$key")"
  case "$readiness_state" in
    present)
      ok "$key exists according to readinessStatus"
      return 0
      ;;
    missing)
      warn "$key is not visible to readinessStatus; checking Secret Manager directly"
      ;;
  esac

  if ! ensure_firebase_cli_ready; then
    if [[ "$required" == "1" ]]; then
      fail "$key cannot be verified because readinessStatus and Firebase CLI access are unavailable"
      return 1
    fi

    warn "$key cannot be verified because readinessStatus and Firebase CLI access are unavailable"
    return 0
  fi

  if run_with_timeout "${FIREBASE_SECRET_ACCESS_TIMEOUT_SECONDS:-12}" \
    firebase functions:secrets:access "$key" --project "$PROJECT_ID" >"$output" 2>"$stderr"; then
    if [[ ! -s "$output" ]]; then
      if [[ "$required" == "1" ]]; then
        fail "$key exists but is empty"
        return 1
      fi

      warn "$key exists but is empty"
      return 0
    fi

    if [[ "$key" == "GOOGLE_SERVICE_ACCOUNT_JSON" ]]; then
      if node -e 'const fs=require("fs"); JSON.parse(fs.readFileSync(process.argv[1], "utf8"));' "$output" >/dev/null 2>&1; then
        ok "$key exists and contains valid JSON"
      else
        fail "$key exists but is not valid JSON"
        return 1
      fi
    else
      ok "$key exists"
    fi

    return 0
  fi

  if [[ "$required" == "1" ]]; then
    fail "$key is missing or inaccessible"
    return 1
  fi

  warn "$key is missing or inaccessible"
  return 0
}

echo "Firebase Functions Secrets check"
echo "Project: $PROJECT_ID"
echo

fetch_readiness_status
if [[ "$READINESS_STATE" == "available" ]]; then
  ok "readinessStatus secret diagnostics available"
else
  warn "readinessStatus secret diagnostics unavailable; falling back to Firebase CLI Secret Manager access"
fi

failures=0
for key in "${REQUIRED_SECRETS[@]}"; do
  if ! check_secret "$key" "1"; then
    failures=$((failures + 1))
  fi
done

if [[ "$failures" -gt 0 ]]; then
  echo
  echo "Summary: $failures missing/invalid required secret(s)"
  exit 1
fi

if [[ "${REQUIRE_PAYMENT_WEBHOOK:-0}" == "1" ]]; then
  if ! check_secret PAYMENT_WEBHOOK_SECRET "1"; then
    failures=$((failures + 1))
  fi
else
  for key in "${OPTIONAL_SECRETS[@]}"; do
    check_secret "$key" "0" || true
  done
fi

echo
if [[ "$failures" -gt 0 ]]; then
  echo "Summary: $failures missing/invalid required secret(s)"
  exit 1
fi

echo "Summary: all required Firebase Functions secrets are available"
