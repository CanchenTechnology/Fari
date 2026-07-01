#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RELEASE_ENV_FILE="${RELEASE_ENV_FILE:-}"

if [[ -n "$RELEASE_ENV_FILE" ]]; then
  if [[ "$RELEASE_ENV_FILE" != /* ]]; then
    RELEASE_ENV_FILE="$ROOT_DIR/$RELEASE_ENV_FILE"
  fi

  if [[ ! -f "$RELEASE_ENV_FILE" ]]; then
    cat >&2 <<EOF
Release env file does not exist: $RELEASE_ENV_FILE

Create it, fill the real local values, then rerun:
  ./scripts/init-release-env.sh
  RELEASE_ENV_FILE=scripts/release.env REPORT_ONLY=1 ./scripts/prepare-release.sh
EOF
    exit 3
  fi

  set -a
  # shellcheck disable=SC1090
  . "$RELEASE_ENV_FILE"
  set +a
  export MOONLY_RELEASE_ENV_FILE="$RELEASE_ENV_FILE"
fi

PROJECT_ID="${FIREBASE_PROJECT:-fari-app-b2fd2}"
DRY_RUN="${DRY_RUN:-0}"
REPORT_ONLY="${REPORT_ONLY:-0}"
RUN_CONFIGURE_GOOGLE_PLAY_GAMES="${RUN_CONFIGURE_GOOGLE_PLAY_GAMES:-auto}"
RUN_ALL_SECRET_SETUP="${RUN_ALL_SECRET_SETUP:-0}"
RUN_IAP_SECRET_SETUP="${RUN_IAP_SECRET_SETUP:-0}"
RUN_PUBLIC_CONFIG_UPDATE="${RUN_PUBLIC_CONFIG_UPDATE:-0}"
PUBLIC_CONFIG_PATH="${PUBLIC_CONFIG_PATH:-functions/public-config.example.json}"
REQUIRE_REAL_SOCIAL_LINKS="${REQUIRE_REAL_SOCIAL_LINKS:-1}"
RUN_DEPLOY="${RUN_DEPLOY:-0}"
RUN_BUILDS="${RUN_BUILDS:-0}"
RUN_IOS_BUILD="${RUN_IOS_BUILD:-$RUN_BUILDS}"
RUN_ANDROID_BUILD="${RUN_ANDROID_BUILD:-$RUN_BUILDS}"
RUN_RELEASE_GATE="${RUN_RELEASE_GATE:-1}"
WAIT_FOR_UNITY_CLOSE="${WAIT_FOR_UNITY_CLOSE:-0}"
UNITY_WAIT_TIMEOUT_SECONDS="${UNITY_WAIT_TIMEOUT_SECONDS:-600}"
UNITY_WAIT_POLL_SECONDS="${UNITY_WAIT_POLL_SECONDS:-5}"

BLOCKERS=0
WARNINGS=0

if [[ -n "${MOONLY_PROXY:-}" ]]; then
  export HTTPS_PROXY="$MOONLY_PROXY"
  export HTTP_PROXY="$MOONLY_PROXY"
fi

if [[ -n "${MOONLY_ALL_PROXY:-}" ]]; then
  export ALL_PROXY="$MOONLY_ALL_PROXY"
fi

usage() {
  cat <<'EOF'
Usage:
  ./scripts/prepare-release.sh

Safe defaults:
  - Runs the release blocker gate.
  - Does not upload secrets.
  - Does not deploy Functions.
  - Does not rebuild iOS/Android artifacts.

Useful modes:
  ./scripts/init-release-env.sh
  REPORT_ONLY=1 ./scripts/prepare-release.sh
  RELEASE_ENV_FILE=scripts/release.env REPORT_ONLY=1 ./scripts/prepare-release.sh
  DRY_RUN=1 RUN_IAP_SECRET_SETUP=1 RUN_DEPLOY=1 RUN_BUILDS=1 ./scripts/prepare-release.sh
  RUN_IAP_SECRET_SETUP=1 RUN_DEPLOY=1 ./scripts/prepare-release.sh
  RUN_PUBLIC_CONFIG_UPDATE=1 PUBLIC_CONFIG_PATH=functions/public-config.live.json ./scripts/prepare-release.sh
  RUN_BUILDS=1 ./scripts/prepare-release.sh
  WAIT_FOR_UNITY_CLOSE=1 RUN_BUILDS=1 ./scripts/prepare-release.sh

External values used when enabled:
  GOOGLE_PLAY_GAMES_APP_ID
  PUBLIC_CONFIG_PATH with real social links / IAP display values when RUN_PUBLIC_CONFIG_UPDATE=1
  APPLE_SHARED_SECRET
  GOOGLE_PACKAGE_NAME
  GOOGLE_SERVICE_ACCOUNT_JSON or GOOGLE_SERVICE_ACCOUNT_JSON_FILE
  IAP_RECEIPT or REAL_IAP_RECEIPT for real sandbox IAP receipt verification
EOF
}

ok() {
  echo "[OK] $*"
}

warn() {
  echo "[WARN] $*"
  WARNINGS=$((WARNINGS + 1))
}

block() {
  echo "[BLOCKED] $*"
  BLOCKERS=$((BLOCKERS + 1))
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

script_exists() {
  local path="$1"
  if [[ -x "$ROOT_DIR/$path" ]]; then
    ok "$path is executable"
  else
    block "$path is missing or not executable"
  fi
}

file_exists() {
  local path="$1"
  if [[ -f "$ROOT_DIR/$path" ]]; then
    ok "$path exists"
  else
    block "$path is missing"
  fi
}

require_env() {
  local key="$1"
  if [[ -n "${!key:-}" ]]; then
    ok "$key is present"
  else
    block "$key is required for this selected release step"
  fi
}

require_google_service_account() {
  if [[ -n "${GOOGLE_SERVICE_ACCOUNT_JSON:-}" || -n "${GOOGLE_SERVICE_ACCOUNT_JSON_FILE:-}" ]]; then
    ok "Google service account JSON source is present"
  else
    block "GOOGLE_SERVICE_ACCOUNT_JSON or GOOGLE_SERVICE_ACCOUNT_JSON_FILE is required for Google IAP receipt validation"
  fi
}

validate_google_play_games_app_id() {
  if DRY_RUN=1 ./scripts/configure-google-play-games.sh >/tmp/moonly_prepare_google_play_games_dry_run.log 2>&1; then
    ok "GOOGLE_PLAY_GAMES_APP_ID format is valid"
  else
    block "GOOGLE_PLAY_GAMES_APP_ID validation failed; see /tmp/moonly_prepare_google_play_games_dry_run.log"
  fi
}

validate_all_secret_inputs() {
  if DRY_RUN=1 ./scripts/setup-firebase-secrets.sh >/tmp/moonly_prepare_all_secrets_dry_run.log 2>&1; then
    ok "all requested Firebase secret inputs are valid"
  else
    block "Firebase secret input validation failed; see /tmp/moonly_prepare_all_secrets_dry_run.log"
  fi
}

validate_iap_secret_inputs() {
  if env MOONLY_SECRET_NAMES=APPLE_SHARED_SECRET,GOOGLE_PACKAGE_NAME,GOOGLE_SERVICE_ACCOUNT_JSON DRY_RUN=1 ./scripts/setup-firebase-secrets.sh >/tmp/moonly_prepare_iap_secrets_dry_run.log 2>&1; then
    ok "IAP Firebase secret inputs are valid"
  else
    block "IAP Firebase secret input validation failed; see /tmp/moonly_prepare_iap_secrets_dry_run.log"
  fi
}

validate_real_iap_receipt_inputs() {
  local receipt="${IAP_RECEIPT:-${REAL_IAP_RECEIPT:-}}"
  local store="${IAP_STORE:-AppleAppStore}"
  local product_id="${IAP_PRODUCT_ID:-fari.pro.monthly}"

  if [[ -z "$receipt" ]]; then
    block "IAP_RECEIPT or REAL_IAP_RECEIPT is required when CHECK_IAP_REAL_RECEIPT=1 or IAP_SMOKE_MODE=real"
    return
  fi

  if [[ -z "$product_id" ]]; then
    block "IAP_PRODUCT_ID is required for real receipt verification"
    return
  fi

  case "$store" in
    AppleAppStore|GooglePlay)
      ok "real IAP receipt inputs are present for $store / $product_id"
      ;;
    *)
      block "IAP_STORE must be AppleAppStore or GooglePlay for real receipt verification: $store"
      ;;
  esac
}

validate_public_config_inputs() {
  local args=(functions/scripts/set-public-config.js --dry-run --project "$PROJECT_ID")

  if truthy "$REQUIRE_REAL_SOCIAL_LINKS"; then
    args+=(--require-real-social-links)
  fi

  args+=("$PUBLIC_CONFIG_PATH")

  if node "${args[@]}" >/tmp/moonly_prepare_public_config_dry_run.log 2>&1; then
    ok "public app config is valid"
  else
    block "public app config validation failed; see /tmp/moonly_prepare_public_config_dry_run.log"
  fi
}

unity_is_open() {
  [[ -f "$ROOT_DIR/Temp/UnityLockfile" ]] \
    && command -v lsof >/dev/null 2>&1 \
    && lsof "$ROOT_DIR/Temp/UnityLockfile" >/dev/null 2>&1
}

wait_for_unity_close() {
  local timeout="$UNITY_WAIT_TIMEOUT_SECONDS"
  local poll="$UNITY_WAIT_POLL_SECONDS"
  local waited=0

  if [[ ! "$timeout" =~ ^[0-9]+$ || "$timeout" -lt 1 ]]; then
    block "UNITY_WAIT_TIMEOUT_SECONDS must be a positive integer: $timeout"
    return 1
  fi

  if [[ ! "$poll" =~ ^[0-9]+$ || "$poll" -lt 1 ]]; then
    block "UNITY_WAIT_POLL_SECONDS must be a positive integer: $poll"
    return 1
  fi

  while unity_is_open; do
    if [[ "$waited" -ge "$timeout" ]]; then
      block "Unity is still open after waiting ${timeout}s; close Unity before batchmode builds"
      return 1
    fi

    echo "[WAIT] Unity is open; waiting ${poll}s before checking again (${waited}/${timeout}s)"
    sleep "$poll"
    waited=$((waited + poll))
  done

  ok "Unity project is not locked for batchmode builds"
}

print_command() {
  printf "%q " "$@"
  echo
}

run_step() {
  local label="$1"
  shift

  echo
  echo "== $label =="
  if truthy "$DRY_RUN"; then
    echo "[DRY-RUN] command:"
    print_command "$@"
    return 0
  fi

  "$@"
}

cd "$ROOT_DIR"

if [[ "${1:-}" == "--help" || "${1:-}" == "-h" ]]; then
  usage
  exit 0
fi

echo "Moonly release preparation"
echo "Project: $ROOT_DIR"
echo "Firebase project: $PROJECT_ID"
echo "Dry run: $DRY_RUN"
if [[ -n "${MOONLY_RELEASE_ENV_FILE:-}" ]]; then
  echo "Release env file: $MOONLY_RELEASE_ENV_FILE"
fi
echo

script_exists "scripts/check-release-blockers.sh"
script_exists "scripts/configure-google-play-games.sh"
script_exists "scripts/setup-firebase-secrets.sh"
script_exists "scripts/deploy-firebase.sh"
file_exists "functions/scripts/set-public-config.js"
script_exists "scripts/build-ios-xcode.sh"
script_exists "scripts/build-android-apk.sh"
echo

configure_google=0
if [[ "$RUN_CONFIGURE_GOOGLE_PLAY_GAMES" == "auto" ]]; then
  if [[ -n "${GOOGLE_PLAY_GAMES_APP_ID:-}" ]]; then
    configure_google=1
  else
    warn "GOOGLE_PLAY_GAMES_APP_ID is not set; Google Play Games configuration will be skipped"
  fi
elif truthy "$RUN_CONFIGURE_GOOGLE_PLAY_GAMES"; then
  configure_google=1
fi

if [[ "$configure_google" == "1" ]]; then
  require_env GOOGLE_PLAY_GAMES_APP_ID
  validate_google_play_games_app_id
fi

if truthy "$RUN_ALL_SECRET_SETUP"; then
  require_env DASHSCOPE_API_KEY
  require_env VOLC_TTS_API_KEY
  require_env APPLE_SHARED_SECRET
  require_env GOOGLE_PACKAGE_NAME
  require_google_service_account
  validate_all_secret_inputs
elif truthy "$RUN_IAP_SECRET_SETUP"; then
  require_env APPLE_SHARED_SECRET
  require_env GOOGLE_PACKAGE_NAME
  require_google_service_account
  validate_iap_secret_inputs
fi

if truthy "$RUN_PUBLIC_CONFIG_UPDATE"; then
  validate_public_config_inputs
fi

if truthy "$RUN_IOS_BUILD" || truthy "$RUN_ANDROID_BUILD"; then
  if truthy "$DRY_RUN"; then
    if unity_is_open; then
      warn "Unity is currently open; dry-run will continue, but real batchmode builds require Unity to be closed or WAIT_FOR_UNITY_CLOSE=1"
    else
      ok "Unity project is not locked for batchmode builds"
    fi
  elif truthy "$WAIT_FOR_UNITY_CLOSE"; then
    wait_for_unity_close || true
  elif unity_is_open; then
    block "Unity currently has this project open; close Unity before RUN_BUILDS/RUN_IOS_BUILD/RUN_ANDROID_BUILD, or set WAIT_FOR_UNITY_CLOSE=1"
  else
    ok "Unity project is not locked for batchmode builds"
  fi
fi

if [[ "${CHECK_IAP_REAL_RECEIPT:-0}" == "1" || "${IAP_SMOKE_MODE:-}" == "real" || -n "${IAP_RECEIPT:-${REAL_IAP_RECEIPT:-}}" ]]; then
  validate_real_iap_receipt_inputs
fi

if [[ "$BLOCKERS" -gt 0 ]]; then
  echo
  echo "Summary: $BLOCKERS blocker(s), $WARNINGS warning(s) before running release preparation steps"
  exit 2
fi

if [[ "$configure_google" == "1" ]]; then
  run_step "Configure Google Play Games APP_ID" ./scripts/configure-google-play-games.sh "$GOOGLE_PLAY_GAMES_APP_ID"
fi

if truthy "$RUN_ALL_SECRET_SETUP"; then
  run_step "Set all Firebase Functions secrets" ./scripts/setup-firebase-secrets.sh
elif truthy "$RUN_IAP_SECRET_SETUP"; then
  run_step \
    "Set IAP Firebase Functions secrets" \
    env MOONLY_SECRET_NAMES=APPLE_SHARED_SECRET,GOOGLE_PACKAGE_NAME,GOOGLE_SERVICE_ACCOUNT_JSON ./scripts/setup-firebase-secrets.sh
fi

if truthy "$RUN_DEPLOY"; then
  run_step "Deploy Firebase with all secret bindings" env MOONLY_BIND_ALL_SECRETS=1 ./scripts/deploy-firebase.sh
fi

if truthy "$RUN_PUBLIC_CONFIG_UPDATE"; then
  public_config_cmd=(node functions/scripts/set-public-config.js --project "$PROJECT_ID")
  if truthy "$REQUIRE_REAL_SOCIAL_LINKS"; then
    public_config_cmd+=(--require-real-social-links)
  fi
  public_config_cmd+=("$PUBLIC_CONFIG_PATH")
  run_step "Update public app config" "${public_config_cmd[@]}"
fi

if truthy "$RUN_IOS_BUILD"; then
  run_step "Build and validate iOS Xcode export" env CLEAN_IOS_EXPORT=1 ./scripts/build-ios-xcode.sh
fi

if truthy "$RUN_ANDROID_BUILD"; then
  run_step "Build and validate Android APK" env CLEAN_ANDROID_BUILD=1 ./scripts/build-android-apk.sh
fi

if truthy "$RUN_RELEASE_GATE"; then
  release_gate_env=(
    CHECK_FIREBASE_SECRETS=1
    CHECK_FUNCTIONS_SMOKE=1
    CHECK_IAP_FAKE_SMOKE=1
  )

  if truthy "$REPORT_ONLY"; then
    release_gate_env+=(ALLOW_RELEASE_BLOCKERS=1)
  fi

  if [[ -n "${IAP_RECEIPT:-${REAL_IAP_RECEIPT:-}}" || "${CHECK_IAP_REAL_RECEIPT:-0}" == "1" ]]; then
    release_gate_env+=(CHECK_IAP_REAL_RECEIPT=1)
  fi

  run_step "Run release blocker gate" env "${release_gate_env[@]}" ./scripts/check-release-blockers.sh
else
  warn "release blocker gate skipped; set RUN_RELEASE_GATE=1 before final verification"
fi

echo
echo "Release preparation flow finished."
