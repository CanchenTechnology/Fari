#!/usr/bin/env bash
set -euo pipefail

PROJECT_ID="${FIREBASE_PROJECT:-fari-app-b2fd2}"
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
API_KEY="${FIREBASE_WEB_API_KEY:-}"
SUBMIT_URL="${SUBMIT_IAP_URL:-https://us-central1-$PROJECT_ID.cloudfunctions.net/submitIapReceipt}"
CURL_MAX_TIME="${CURL_MAX_TIME:-60}"
CURL_CONNECT_TIMEOUT="${CURL_CONNECT_TIMEOUT:-20}"
CURL_RETRY="${CURL_RETRY:-2}"
CURL_RETRY_DELAY="${CURL_RETRY_DELAY:-2}"
IAP_RECEIPT="${IAP_RECEIPT:-${REAL_IAP_RECEIPT:-}}"
IAP_PRODUCT_ID="${IAP_PRODUCT_ID:-fari.pro.monthly}"
IAP_STORE="${IAP_STORE:-AppleAppStore}"
IAP_PLATFORM="${IAP_PLATFORM:-smoke-test}"
IAP_APP_VERSION="${IAP_APP_VERSION:-smoke}"
IAP_TRANSACTION_ID="${IAP_TRANSACTION_ID:-}"
IAP_PACKAGE_NAME="${IAP_PACKAGE_NAME:-}"
IAP_SMOKE_MODE="${IAP_SMOKE_MODE:-}"

if [[ -z "$IAP_SMOKE_MODE" ]]; then
  if [[ -n "$IAP_RECEIPT" ]]; then
    IAP_SMOKE_MODE="real"
  else
    IAP_SMOKE_MODE="fake"
  fi
fi

if [[ -z "${CLEANUP_AUTH_USER+x}" ]]; then
  if [[ "$IAP_SMOKE_MODE" == "real" ]]; then
    CLEANUP_AUTH_USER="0"
  else
    CLEANUP_AUTH_USER="1"
  fi
fi

if [[ -n "${MOONLY_PROXY:-}" ]]; then
  export HTTPS_PROXY="$MOONLY_PROXY"
  export HTTP_PROXY="$MOONLY_PROXY"
fi

if [[ -n "${MOONLY_ALL_PROXY:-}" ]]; then
  export ALL_PROXY="$MOONLY_ALL_PROXY"
fi

if ! command -v node >/dev/null 2>&1; then
  echo "node not found." >&2
  exit 127
fi

if ! command -v curl >/dev/null 2>&1; then
  echo "curl not found." >&2
  exit 127
fi

if [[ -z "$API_KEY" ]]; then
  API_KEY="$(
    node - "$ROOT_DIR" <<'NODE'
const fs = require("fs");
const path = require("path");
const root = process.argv[2];
const candidates = [
  "Assets/google-services.json",
  "Assets/StreamingAssets/google-services-desktop.json",
];

for (const relativePath of candidates) {
  const fullPath = path.join(root, relativePath);
  if (!fs.existsSync(fullPath)) continue;
  const data = JSON.parse(fs.readFileSync(fullPath, "utf8"));
  const clients = data.client || [];
  for (const client of clients) {
    const keys = client.api_key || [];
    for (const key of keys) {
      if (key.current_key) {
        process.stdout.write(key.current_key);
        process.exit(0);
      }
    }
  }
}
process.exit(1);
NODE
  )" || {
    echo "Firebase Web API key not found. Set FIREBASE_WEB_API_KEY or keep Assets/google-services.json available." >&2
    exit 2
  }
fi

TMP_DIR="$(mktemp -d)"
ID_TOKEN=""
LOCAL_ID=""

curl_http() {
  local output="$1"
  shift

  local status_file="$TMP_DIR/curl-status-$RANDOM.txt"
  local error_file="$TMP_DIR/curl-error-$RANDOM.txt"
  local exit_code=0
  local http_status

  curl \
    --silent \
    --show-error \
    --location \
    --max-time "$CURL_MAX_TIME" \
    --connect-timeout "$CURL_CONNECT_TIMEOUT" \
    --retry "$CURL_RETRY" \
    --retry-all-errors \
    --retry-delay "$CURL_RETRY_DELAY" \
    "$@" \
    --output "$output" \
    --write-out "%{http_code}" \
    >"$status_file" \
    2>"$error_file" || exit_code=$?

  http_status="$(tr -d '\r\n' <"$status_file" 2>/dev/null || true)"
  [[ -f "$output" ]] || : >"$output"

  if [[ "$exit_code" -ne 0 || ! "$http_status" =~ ^[0-9][0-9][0-9]$ ]]; then
    echo "curl request failed with exit $exit_code; treating as HTTP 000." >&2
    if [[ -s "$error_file" ]]; then
      sed -n '1,8p' "$error_file" >&2
    fi
    http_status="000"
  fi

  rm -f "$status_file" "$error_file"
  printf "%s" "$http_status"
}

cleanup_smoke() {
  local status=$?
  if [[ "$CLEANUP_AUTH_USER" == "1" && -n "${ID_TOKEN:-}" ]]; then
    local delete_body="$TMP_DIR/delete-auth.json"
    local delete_http
    delete_http="$(
      curl_http "$delete_body" \
        --request POST \
        --header "Content-Type: application/json" \
        --data "{\"idToken\":\"$ID_TOKEN\"}" \
        "https://identitytoolkit.googleapis.com/v1/accounts:delete?key=$API_KEY"
    )" || delete_http="000"

    if [[ "$delete_http" == "200" ]]; then
      echo "smoke auth user cleanup: ok"
    else
      echo "smoke auth user cleanup: HTTP $delete_http" >&2
    fi
  fi

  rm -rf "$TMP_DIR"
  exit "$status"
}
trap cleanup_smoke EXIT

AUTH_BODY="$TMP_DIR/auth.json"
AUTH_HTTP="$(
  curl_http "$AUTH_BODY" \
    --request POST \
    --header "Content-Type: application/json" \
    --data '{"returnSecureToken":true}' \
    "https://identitytoolkit.googleapis.com/v1/accounts:signUp?key=$API_KEY"
)"

if [[ "$AUTH_HTTP" != "200" ]]; then
  if grep -q "ADMIN_ONLY_OPERATION" "$AUTH_BODY"; then
    TEST_EMAIL="moonly-smoke-$(date +%s)-$RANDOM@example.invalid"
    TEST_PASSWORD="MoonlySmoke${RANDOM}!"
    echo "anonymous auth is disabled; creating temporary email/password smoke user"
    AUTH_HTTP="$(
      curl_http "$AUTH_BODY" \
        --request POST \
        --header "Content-Type: application/json" \
        --data "{\"email\":\"$TEST_EMAIL\",\"password\":\"$TEST_PASSWORD\",\"returnSecureToken\":true}" \
        "https://identitytoolkit.googleapis.com/v1/accounts:signUp?key=$API_KEY"
    )"
  fi

  if [[ "$AUTH_HTTP" != "200" ]]; then
    echo "Firebase smoke sign-in failed with HTTP $AUTH_HTTP:" >&2
    sed -n '1,20p' "$AUTH_BODY" >&2
    exit 3
  fi
fi

ID_TOKEN="$(node -e 'const fs=require("fs"); const j=JSON.parse(fs.readFileSync(process.argv[1],"utf8")); process.stdout.write(j.idToken || "");' "$AUTH_BODY")"
LOCAL_ID="$(node -e 'const fs=require("fs"); const j=JSON.parse(fs.readFileSync(process.argv[1],"utf8")); process.stdout.write(j.localId || "");' "$AUTH_BODY")"

if [[ -z "$ID_TOKEN" ]]; then
  echo "Anonymous Firebase sign-in did not return idToken." >&2
  exit 3
fi

if [[ "$IAP_SMOKE_MODE" == "real" && -z "$IAP_RECEIPT" ]]; then
  echo "IAP_SMOKE_MODE=real requires IAP_RECEIPT or REAL_IAP_RECEIPT." >&2
  exit 4
fi

if [[ "$IAP_SMOKE_MODE" != "real" && "$IAP_SMOKE_MODE" != "fake" ]]; then
  echo "Unsupported IAP_SMOKE_MODE: $IAP_SMOKE_MODE. Use fake or real." >&2
  exit 4
fi

SUBMIT_PAYLOAD="$(
  node - "$IAP_SMOKE_MODE" "$IAP_PRODUCT_ID" "$IAP_RECEIPT" "$IAP_STORE" "$IAP_PLATFORM" "$IAP_APP_VERSION" "$IAP_TRANSACTION_ID" "$IAP_PACKAGE_NAME" <<'NODE'
const mode = process.argv[2];
const productId = process.argv[3];
const receipt = process.argv[4];
const store = process.argv[5];
const platform = process.argv[6];
const appVersion = process.argv[7];
const transactionId = process.argv[8];
const packageName = process.argv[9];

const payload = mode === "real"
  ? {
      productId,
      receipt,
      store,
      platform,
      appVersion,
      transactionId,
      packageName,
    }
  : {
      productId,
      receipt: "{\"Store\":\"AppleAppStore\",\"Payload\":\"moonly-smoke-receipt\"}",
      store: "AppleAppStore",
      platform: "smoke-test",
      appVersion: "smoke",
    };

for (const key of Object.keys(payload)) {
  if (payload[key] === "") {
    delete payload[key];
  }
}

process.stdout.write(JSON.stringify(payload));
NODE
)"

RECEIPT_BODY="$TMP_DIR/submit-iap.json"
RECEIPT_HTTP="$(
  curl_http "$RECEIPT_BODY" \
    --request POST \
    --header "Content-Type: application/json" \
    --header "Authorization: Bearer $ID_TOKEN" \
    --data "$SUBMIT_PAYLOAD" \
    "$SUBMIT_URL"
)"

node - "$RECEIPT_HTTP" "$RECEIPT_BODY" "$LOCAL_ID" "$IAP_SMOKE_MODE" <<'NODE'
const fs = require("fs");
const httpStatus = process.argv[2];
const bodyPath = process.argv[3];
const localId = process.argv[4];
const mode = process.argv[5];
const body = fs.readFileSync(bodyPath, "utf8");
let data;
try {
  data = JSON.parse(body);
} catch (error) {
  console.error(`submitIapReceipt returned invalid JSON with HTTP ${httpStatus}: ${body.slice(0, 500)}`);
  process.exit(4);
}

console.log(`anonymous uid: ${localId}`);
console.log(`iap smoke mode: ${mode}`);
console.log(`submitIapReceipt HTTP: ${httpStatus}`);
console.log(`receipt status: ${data.status || ""}`);
console.log(`receipt id: ${data.receiptId || ""}`);
console.log(`membership status: ${data.membershipStatus || ""}`);
console.log(`is pro: ${data.isPro === true ? "true" : "false"}`);
console.log(`message: ${data.message || data.error || ""}`);

if (mode === "real") {
  if (httpStatus !== "200" || data.status !== "verified" || data.isPro !== true || data.membershipStatus !== "pro") {
    console.error("Expected real IAP receipt to return HTTP 200 with status verified and Pro membership.");
    process.exit(4);
  }
  process.exit(0);
}

const acceptedFakeStates = new Set(["pending_configuration", "invalid"]);
if (!acceptedFakeStates.has(data.status) || !["202", "402"].includes(httpStatus)) {
  console.error("Expected fake receipt to return pending_configuration (missing IAP secrets) or invalid (IAP secrets configured).");
  process.exit(4);
}

if (httpStatus === "200" || data.status === "verified" || data.isPro === true || data.membershipStatus === "pro") {
  console.error("Fake receipt unexpectedly verified or granted Pro.");
  process.exit(4);
}
NODE
