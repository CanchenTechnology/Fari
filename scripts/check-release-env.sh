#!/usr/bin/env bash
set -u

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ENV_FILE_INPUT="${RELEASE_ENV_FILE:-scripts/release.env}"
FAILURES=0
WARNINGS=0
OKS=0

usage() {
  cat <<'EOF'
Usage:
  ./scripts/check-release-env.sh
  RELEASE_ENV_FILE=scripts/release.env ./scripts/check-release-env.sh
  ./scripts/check-release-env.sh --env-file scripts/release.env
  ./scripts/check-release-env.sh --no-env-file

Checks local release inputs without uploading secrets, deploying, building, or printing secret values.

Before first use:
  ./scripts/init-release-env.sh
EOF
}

ok() {
  echo "[OK] $*"
  OKS=$((OKS + 1))
}

warn() {
  echo "[WARN] $*"
  WARNINGS=$((WARNINGS + 1))
}

fail() {
  echo "[FAIL] $*"
  FAILURES=$((FAILURES + 1))
}

truthy() {
  case "${1:-0}" in
    1|true|TRUE|yes|YES|on|ON)
      return 0
      ;;
    *)
      return 1
      ;;
  esac
}

is_placeholder() {
  local value="${1:-}"
  [[ -z "$value" ]] && return 0
  case "$value" in
    *"<"*">"*|*"/path/to/"*|*"/absolute/path/"*|"..."|"TODO"|"todo"|"changeme"|"CHANGE_ME"|"dry-run"*|"dry_run"*)
      return 0
      ;;
    *)
      return 1
      ;;
  esac
}

require_secret_value() {
  local key="$1"
  local value="${!key:-}"

  if is_placeholder "$value"; then
    fail "$key is missing or still a placeholder"
  else
    ok "$key is present"
  fi
}

validate_apple_shared_secret() {
  local value="${APPLE_SHARED_SECRET:-}"

  if is_placeholder "$value"; then
    fail "APPLE_SHARED_SECRET is missing or still a placeholder"
    return
  fi

  if [[ "$value" =~ [[:space:]] ]]; then
    fail "APPLE_SHARED_SECRET must not contain whitespace"
    return
  fi

  if [[ "${#value}" -lt 16 ]]; then
    fail "APPLE_SHARED_SECRET looks too short"
  else
    ok "APPLE_SHARED_SECRET format looks usable"
  fi
}

validate_package_name() {
  local value="${GOOGLE_PACKAGE_NAME:-}"
  local project_settings="$ROOT_DIR/ProjectSettings/ProjectSettings.asset"
  local android_bundle=""

  if is_placeholder "$value"; then
    fail "GOOGLE_PACKAGE_NAME is missing or still a placeholder"
    return
  fi

  if node - "$value" <<'NODE' >/dev/null 2>&1
const packageName = process.argv[2] || "";
const ok = /^[A-Za-z][A-Za-z0-9_]*(\.[A-Za-z][A-Za-z0-9_]*)+$/.test(packageName);
process.exit(ok ? 0 : 1);
NODE
  then
    ok "GOOGLE_PACKAGE_NAME format is valid"
  else
    fail "GOOGLE_PACKAGE_NAME format is invalid"
    return
  fi

  if [[ -f "$project_settings" ]]; then
    android_bundle="$(node - "$project_settings" <<'NODE'
const fs = require("fs");
const path = process.argv[2];
const text = fs.readFileSync(path, "utf8");
const match = text.match(/applicationIdentifier:\s*\n(?:.*\n)*?\s+Android:\s*([^\s]+)/);
if (match) process.stdout.write(match[1]);
NODE
)"
  fi

  if [[ -z "$android_bundle" ]]; then
    warn "ProjectSettings Android bundle id could not be read; GOOGLE_PACKAGE_NAME match not verified"
  elif [[ "$value" == "$android_bundle" ]]; then
    ok "GOOGLE_PACKAGE_NAME matches ProjectSettings Android bundle id"
  else
    fail "GOOGLE_PACKAGE_NAME ($value) does not match ProjectSettings Android bundle id ($android_bundle)"
  fi
}

validate_google_service_account() {
  if [[ -n "${GOOGLE_SERVICE_ACCOUNT_JSON_FILE:-}" ]]; then
    local file="$GOOGLE_SERVICE_ACCOUNT_JSON_FILE"
    if [[ "$file" != /* ]]; then
      file="$ROOT_DIR/$file"
    fi

    if is_placeholder "$GOOGLE_SERVICE_ACCOUNT_JSON_FILE"; then
      fail "GOOGLE_SERVICE_ACCOUNT_JSON_FILE is still a placeholder"
      return
    fi

    if [[ ! -f "$file" ]]; then
      fail "GOOGLE_SERVICE_ACCOUNT_JSON_FILE does not exist"
      return
    fi

    if node - "$file" <<'NODE' >/dev/null 2>&1
const fs = require("fs");
const path = process.argv[2];
const data = JSON.parse(fs.readFileSync(path, "utf8"));
if (data.type !== "service_account") throw new Error("type must be service_account");
if (!data.client_email) throw new Error("client_email missing");
if (!data.client_email.endsWith(".gserviceaccount.com")) throw new Error("client_email must be a service account");
if (!data.private_key) throw new Error("private_key missing");
if (!data.private_key.includes("BEGIN PRIVATE KEY")) throw new Error("private_key must be a PEM private key");
NODE
    then
      ok "GOOGLE_SERVICE_ACCOUNT_JSON_FILE is valid service account JSON"
    else
      fail "GOOGLE_SERVICE_ACCOUNT_JSON_FILE is not valid service account JSON"
    fi
    return
  fi

  if is_placeholder "${GOOGLE_SERVICE_ACCOUNT_JSON:-}"; then
    fail "GOOGLE_SERVICE_ACCOUNT_JSON or GOOGLE_SERVICE_ACCOUNT_JSON_FILE is missing"
    return
  fi

  if GOOGLE_SERVICE_ACCOUNT_JSON="$GOOGLE_SERVICE_ACCOUNT_JSON" node <<'NODE' >/dev/null 2>&1
const data = JSON.parse(process.env.GOOGLE_SERVICE_ACCOUNT_JSON || "");
if (data.type !== "service_account") throw new Error("type must be service_account");
if (!data.client_email) throw new Error("client_email missing");
if (!data.client_email.endsWith(".gserviceaccount.com")) throw new Error("client_email must be a service account");
if (!data.private_key) throw new Error("private_key missing");
if (!data.private_key.includes("BEGIN PRIVATE KEY")) throw new Error("private_key must be a PEM private key");
NODE
  then
    ok "GOOGLE_SERVICE_ACCOUNT_JSON is valid service account JSON"
  else
    fail "GOOGLE_SERVICE_ACCOUNT_JSON is not valid service account JSON"
  fi
}

validate_receipt_inputs() {
  local receipt="${IAP_RECEIPT:-${REAL_IAP_RECEIPT:-}}"
  local store="${IAP_STORE:-AppleAppStore}"
  local product_id="${IAP_PRODUCT_ID:-fair.pro.monthly}"

  if is_placeholder "$receipt"; then
    fail "IAP_RECEIPT or REAL_IAP_RECEIPT is missing or still a placeholder"
  else
    if node - "$receipt" "$store" <<'NODE' >/dev/null 2>&1
const receipt = process.argv[2] || "";
const expectedStore = process.argv[3] || "AppleAppStore";

if (receipt.length < 20) {
  throw new Error("receipt looks too short");
}
if (/moonly-smoke-receipt|fake|placeholder|TODO/i.test(receipt)) {
  throw new Error("receipt looks fake");
}

const trimmed = receipt.trim();
if (trimmed.startsWith("{")) {
  const data = JSON.parse(trimmed);
  const store = data.Store || data.store || "";
  if (store && store !== expectedStore) {
    throw new Error("receipt store mismatch");
  }
  const payload = data.Payload || data.payload || data.receipt || "";
  if (!payload || String(payload).length < 10) {
    throw new Error("Unity receipt JSON is missing a usable Payload");
  }
}
NODE
    then
      ok "real sandbox IAP receipt input format looks usable"
    else
      fail "IAP_RECEIPT or REAL_IAP_RECEIPT does not look like a real receipt for the selected store"
    fi
  fi

  if is_placeholder "$product_id"; then
    fail "IAP_PRODUCT_ID is missing or still a placeholder"
  else
    ok "IAP_PRODUCT_ID is present"
  fi

  case "$store" in
    AppleAppStore|GooglePlay)
      ok "IAP_STORE is supported: $store"
      ;;
    *)
      fail "IAP_STORE must be AppleAppStore or GooglePlay"
      ;;
  esac
}

validate_android_keystore() {
  if truthy "${SKIP_ANDROID_KEYSTORE_VALIDATION:-0}"; then
    warn "Android keystore password validation skipped"
    return
  fi

  if is_placeholder "${ANDROID_KEYSTORE_PASS:-}" || is_placeholder "${ANDROID_KEYALIAS_PASS:-}"; then
    return
  fi

  if "$ROOT_DIR/scripts/check-android-keystore.sh" >/tmp/moonly_release_env_android_keystore.log 2>&1; then
    ok "Android keystore passwords are valid for the configured alias"
  else
    fail "Android keystore password validation failed; see /tmp/moonly_release_env_android_keystore.log"
  fi
}

validate_public_config() {
  if ! truthy "${RUN_PUBLIC_CONFIG_UPDATE:-0}"; then
    return
  fi

  local public_config_path="${PUBLIC_CONFIG_PATH:-functions/public-config.example.json}"
  local args=("functions/scripts/set-public-config.js" "--dry-run" "--project" "${FIREBASE_PROJECT:-fari-app-b2fd2}")

  if truthy "${REQUIRE_REAL_SOCIAL_LINKS:-1}"; then
    args+=("--require-real-social-links")
  fi

  args+=("$public_config_path")

  if (cd "$ROOT_DIR" && node "${args[@]}") >/tmp/moonly_release_env_public_config.log 2>&1; then
    ok "public app config is valid"
  else
    fail "public app config validation failed; see /tmp/moonly_release_env_public_config.log"
  fi
}

while [[ "${1:-}" != "" ]]; do
  case "$1" in
    --env-file)
      if [[ -z "${2:-}" ]]; then
        echo "--env-file requires a path" >&2
        exit 2
      fi
      ENV_FILE_INPUT="$2"
      shift 2
      ;;
    --no-env-file)
      ENV_FILE_INPUT=""
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown argument: $1" >&2
      usage >&2
      exit 2
      ;;
  esac
done

if [[ -n "$ENV_FILE_INPUT" && "$ENV_FILE_INPUT" != /* ]]; then
  ENV_FILE_INPUT="$ROOT_DIR/$ENV_FILE_INPUT"
fi

echo "Moonly release env check"
if [[ -n "$ENV_FILE_INPUT" ]]; then
  echo "Release env file: $ENV_FILE_INPUT"
else
  echo "Release env file: <none; using current environment>"
fi
echo

if [[ -n "$ENV_FILE_INPUT" ]]; then
  if [[ ! -f "$ENV_FILE_INPUT" ]]; then
    fail "release env file does not exist; create it with ./scripts/init-release-env.sh"
  else
    ok "release env file exists"
    set -a
    # shellcheck disable=SC1090
    . "$ENV_FILE_INPUT"
    set +a
  fi
fi

require_secret_value ANDROID_KEYSTORE_PASS
require_secret_value ANDROID_KEYALIAS_PASS
validate_android_keystore
validate_apple_shared_secret
validate_package_name
validate_google_service_account
validate_receipt_inputs
validate_public_config

if [[ "${REQUIRE_GOOGLE_PLAY_GAMES:-0}" == "1" ]]; then
  require_secret_value GOOGLE_PLAY_GAMES_APP_ID
else
  if is_placeholder "${GOOGLE_PLAY_GAMES_APP_ID:-}"; then
    warn "GOOGLE_PLAY_GAMES_APP_ID is not set; current Google login does not require Play Games"
  else
    ok "GOOGLE_PLAY_GAMES_APP_ID is present"
  fi
fi

echo
echo "Summary: $FAILURES failure(s), $WARNINGS warning(s), $OKS ok check(s)"
if [[ "$FAILURES" -gt 0 ]]; then
  exit 1
fi

exit 0
