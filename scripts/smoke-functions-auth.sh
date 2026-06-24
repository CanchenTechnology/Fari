#!/usr/bin/env bash
set -euo pipefail

PROJECT_ID="${FIREBASE_PROJECT:-fari-app-b2fd2}"
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
API_KEY="${FIREBASE_WEB_API_KEY:-}"
BASE_URL="${FUNCTIONS_BASE_URL:-https://us-central1-$PROJECT_ID.cloudfunctions.net}"
CLEANUP_AUTH_USER="${CLEANUP_AUTH_USER:-1}"
CURL_MAX_TIME="${CURL_MAX_TIME:-60}"
CURL_CONNECT_TIMEOUT="${CURL_CONNECT_TIMEOUT:-20}"
CURL_RETRY="${CURL_RETRY:-2}"
CURL_RETRY_DELAY="${CURL_RETRY_DELAY:-2}"
REQUIRE_AI_TTS_LIVE="${REQUIRE_AI_TTS_LIVE:-0}"

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
    TEST_EMAIL="moonly-functions-smoke-$(date +%s)-$RANDOM@example.invalid"
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
  echo "Firebase smoke sign-in did not return idToken." >&2
  exit 3
fi

echo "smoke uid: $LOCAL_ID"

call_json() {
  local name="$1"
  local method="$2"
  local url="$3"
  local data="${4:-}"
  local output="$TMP_DIR/$name.json"
  local http_status

  if [[ "$method" == "GET" ]]; then
    http_status="$(
      curl_http "$output" \
        --request GET \
        --header "Authorization: Bearer $ID_TOKEN" \
        "$url"
    )"
  else
    http_status="$(
      curl_http "$output" \
        --request "$method" \
        --header "Content-Type: application/json" \
        --header "Authorization: Bearer $ID_TOKEN" \
        --data "$data" \
        "$url"
    )"
  fi

  node - "$name" "$http_status" "$output" <<'NODE'
const fs = require("fs");
const name = process.argv[2];
const httpStatus = process.argv[3];
const path = process.argv[4];
const body = fs.readFileSync(path, "utf8");
let data;
try {
  data = JSON.parse(body);
} catch (error) {
  console.error(`${name} returned invalid JSON with HTTP ${httpStatus}: ${body.slice(0, 500)}`);
  process.exit(10);
}

function fail(message) {
  console.error(`${name} smoke failed: ${message}`);
  console.error(JSON.stringify(data, null, 2).slice(0, 1000));
  process.exit(10);
}

if (name === "publicConfig") {
  if (httpStatus !== "200") fail(`expected HTTP 200, got ${httpStatus}`);
  if (!data.socialLinks || !data.iapProducts) fail("missing socialLinks or iapProducts");
  const monthly = data.iapProducts.proMonthly || {};
  const yearly = data.iapProducts.proYearly || {};
  if (monthly.productId !== "fari.pro.monthly") fail(`unexpected monthly productId: ${monthly.productId || ""}`);
  if (yearly.productId !== "fari.pro.yearly") fail(`unexpected yearly productId: ${yearly.productId || ""}`);
  if (monthly.type !== "subscription" || yearly.type !== "subscription") fail("expected subscription IAP products");
} else if (name === "membershipStatus") {
  if (httpStatus !== "200") fail(`expected HTTP 200, got ${httpStatus}`);
  if (data.membershipStatus !== "free" || data.isPro !== false) {
    fail("expected temporary smoke user to be free");
  }
} else if (name === "aiChat") {
  if (httpStatus === "200" && typeof data.content === "string" && data.content.trim().length > 0) {
    // Secret is configured and live.
  } else if (process.env.REQUIRE_AI_TTS_LIVE !== "1" && httpStatus === "500" && String(data.error || "").includes("DEEPSEEK_API_KEY")) {
    // Expected while the AI secret is intentionally missing.
  } else if (httpStatus === "429") {
    fail("unexpected quota exhaustion for a temporary smoke user");
  } else {
    fail(`unexpected HTTP ${httpStatus}`);
  }
} else if (name === "ttsSynthesize") {
  if (httpStatus === "200" && (data.audioBase64 || data.audio || data.url)) {
    // Secret is configured and live.
  } else if (process.env.REQUIRE_AI_TTS_LIVE !== "1" && httpStatus === "500" && String(data.error || "").includes("VOLC_TTS_API_KEY")) {
    // Expected while the TTS secret is intentionally missing.
  } else if (httpStatus === "429") {
    fail("unexpected quota exhaustion for a temporary smoke user");
  } else {
    fail(`unexpected HTTP ${httpStatus}`);
  }
}

console.log(`${name}: HTTP ${httpStatus} ok`);
NODE
}

call_json "publicConfig" "GET" "$BASE_URL/publicConfig"
call_json "membershipStatus" "GET" "$BASE_URL/membershipStatus"
call_json "aiChat" "POST" "$BASE_URL/aiChat" '{"messages":[{"role":"user","content":"smoke test"}],"max_tokens":16}'
call_json "ttsSynthesize" "POST" "$BASE_URL/ttsSynthesize" '{"text":"smoke test","encoding":"mp3"}'
