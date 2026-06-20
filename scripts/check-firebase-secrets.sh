#!/usr/bin/env bash
set -euo pipefail

PROJECT_ID="${FIREBASE_PROJECT:-fari-app-b2fd2}"
READINESS_URL="${READINESS_URL:-https://us-central1-$PROJECT_ID.cloudfunctions.net/readinessStatus}"
TMP_DIR="$(mktemp -d)"
READINESS_JSON="$TMP_DIR/readiness.json"
READINESS_STATE="unfetched"

REQUIRED_SECRETS=(
  DEEPSEEK_API_KEY
  VOLC_TTS_API_KEY
  APPLE_SHARED_SECRET
  GOOGLE_PACKAGE_NAME
  GOOGLE_SERVICE_ACCOUNT_JSON
)

OPTIONAL_SECRETS=(
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

require_command firebase
require_command node

fetch_readiness_status() {
  if [[ "$READINESS_STATE" != "unfetched" ]]; then
    return
  fi

  READINESS_STATE="unavailable"
  if ! command -v curl >/dev/null 2>&1; then
    return
  fi

  if ! curl -fsS --max-time "${READINESS_TIMEOUT_SECONDS:-20}" "$READINESS_URL" >"$READINESS_JSON" 2>"$TMP_DIR/readiness.err"; then
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
  DEEPSEEK_API_KEY: "deepseekApiKey",
  VOLC_TTS_API_KEY: "volcanoTtsApiKey",
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

if ! firebase login:list --json >/tmp/moonly_secrets_login.json 2>/tmp/moonly_secrets_login.err; then
  fail "Firebase CLI is not logged in; run firebase login --reauth"
  exit 2
fi

if ! node -e 'const fs=require("fs"); const data=JSON.parse(fs.readFileSync("/tmp/moonly_secrets_login.json","utf8")); process.exit(Array.isArray(data.result)&&data.result.length>0?0:1);' >/dev/null 2>&1; then
  fail "Firebase CLI has no active login; run firebase login --reauth"
  exit 2
fi

if ! firebase use --json >/tmp/moonly_secrets_use.json 2>/tmp/moonly_secrets_use.err; then
  fail "Firebase project is not selected; run firebase use $PROJECT_ID"
  exit 2
fi

if ! node - "$PROJECT_ID" <<'NODE' >/dev/null 2>&1
const fs = require("fs");
const expectedProject = process.argv[2];
const data = JSON.parse(fs.readFileSync("/tmp/moonly_secrets_use.json", "utf8"));
process.exit(data.result === expectedProject ? 0 : 1);
NODE
then
  fail "Firebase active project is not $PROJECT_ID; run firebase use $PROJECT_ID"
  exit 2
fi

check_secret() {
  local key="$1"
  local required="$2"
  local output="$TMP_DIR/$key.value"
  local stderr="$TMP_DIR/$key.err"

  if firebase functions:secrets:access "$key" --project "$PROJECT_ID" >"$output" 2>"$stderr"; then
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

  local readiness_state
  readiness_state="$(readiness_secret_state "$key")"
  case "$readiness_state" in
    present)
      ok "$key exists according to readinessStatus (Firebase CLI could not directly access Secret Manager)"
      return 0
      ;;
    missing)
      if [[ "$required" == "1" ]]; then
        fail "$key is missing according to readinessStatus"
        return 1
      fi

      warn "$key is missing according to readinessStatus"
      return 0
      ;;
  esac

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

failures=0
for key in "${REQUIRED_SECRETS[@]}"; do
  if ! check_secret "$key" "1"; then
    failures=$((failures + 1))
  fi
done

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
