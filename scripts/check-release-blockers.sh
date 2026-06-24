#!/usr/bin/env bash
set -u

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
  RELEASE_ENV_FILE=scripts/release.env ./scripts/check-release-blockers.sh
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
IOS_EXPORT_PATH="${IOS_EXPORT_PATH:-Builds/iOS}"
ANDROID_APK_PATH="${ANDROID_APK_PATH:-Builds/Android/MoonlyApp.apk}"
PUBLIC_CONFIG_PATH="${PUBLIC_CONFIG_PATH:-functions/public-config.example.json}"
REQUIRE_REAL_SOCIAL_LINKS="${REQUIRE_REAL_SOCIAL_LINKS:-1}"
LOG_DIR="${LOG_DIR:-/tmp/moonly_release_blockers}"

BLOCKERS=0
WARNINGS=0
OKS=0
REMOTE_SECRETS_CHECKED=0
REMOTE_MISSING_SECRET_NAMES=""
ANDROID_REBUILD_NEEDED=0

if [[ -n "${MOONLY_PROXY:-}" ]]; then
  export HTTPS_PROXY="$MOONLY_PROXY"
  export HTTP_PROXY="$MOONLY_PROXY"
fi

if [[ -n "${MOONLY_ALL_PROXY:-}" ]]; then
  export ALL_PROXY="$MOONLY_ALL_PROXY"
fi

case "${REPORT_ONLY:-0}" in
  1|true|TRUE|yes|YES|on|ON)
    ALLOW_RELEASE_BLOCKERS="${ALLOW_RELEASE_BLOCKERS:-1}"
    ;;
esac

mkdir -p "$LOG_DIR"

ok() {
  echo "[OK] $*"
  OKS=$((OKS + 1))
}

warn() {
  echo "[WARN] $*"
  WARNINGS=$((WARNINGS + 1))
}

block() {
  echo "[BLOCKED] $*"
  BLOCKERS=$((BLOCKERS + 1))
}

log_path() {
  local name="$1"
  printf "%s/%s.log\n" "$LOG_DIR" "$name"
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

add_remote_missing_secret() {
  local key="$1"

  case " $REMOTE_MISSING_SECRET_NAMES " in
    *" $key "*)
      return
      ;;
  esac

  REMOTE_MISSING_SECRET_NAMES="${REMOTE_MISSING_SECRET_NAMES:+$REMOTE_MISSING_SECRET_NAMES }$key"
}

collect_remote_missing_secrets() {
  local log_file
  log_file="$(log_path "firebase-secrets")"

  REMOTE_SECRETS_CHECKED=1
  REMOTE_MISSING_SECRET_NAMES=""

  [[ -f "$log_file" ]] || return

  while IFS= read -r line; do
    case "$line" in
      "[FAIL] APPLE_SHARED_SECRET "*)
        add_remote_missing_secret "APPLE_SHARED_SECRET"
        ;;
      "[FAIL] GOOGLE_PACKAGE_NAME "*)
        add_remote_missing_secret "GOOGLE_PACKAGE_NAME"
        ;;
      "[FAIL] GOOGLE_SERVICE_ACCOUNT_JSON "*)
        add_remote_missing_secret "GOOGLE_SERVICE_ACCOUNT_JSON"
        ;;
    esac
  done <"$log_file"
}

join_by_comma() {
  local IFS=,
  echo "$*"
}

print_iap_secret_unblock_command() {
  local missing=()
  local key
  local has_apple=0
  local has_google_package=0
  local has_google_service_account=0

  if [[ "$REMOTE_SECRETS_CHECKED" == "1" ]]; then
    if [[ -n "$REMOTE_MISSING_SECRET_NAMES" ]]; then
      # shellcheck disable=SC2206
      missing=($REMOTE_MISSING_SECRET_NAMES)
    fi
  else
    missing=(APPLE_SHARED_SECRET GOOGLE_PACKAGE_NAME GOOGLE_SERVICE_ACCOUNT_JSON)
  fi

  if [[ "${#missing[@]}" -eq 0 ]]; then
    echo "     Remote IAP secrets are present. Redeploy Functions only if secret bindings changed:"
    echo "     MOONLY_BIND_ALL_SECRETS=1 ./scripts/deploy-firebase.sh"
    return
  fi

  for key in "${missing[@]}"; do
    case "$key" in
      APPLE_SHARED_SECRET)
        has_apple=1
        ;;
      GOOGLE_PACKAGE_NAME)
        has_google_package=1
        ;;
      GOOGLE_SERVICE_ACCOUNT_JSON)
        has_google_service_account=1
        ;;
    esac
  done

  printf "     MOONLY_SECRET_NAMES=%s" "$(join_by_comma "${missing[@]}")"
  if [[ "$has_apple" == "1" ]]; then
    printf " APPLE_SHARED_SECRET=<secret>"
  fi
  if [[ "$has_google_package" == "1" ]]; then
    printf " GOOGLE_PACKAGE_NAME=com.canchentechnology.fari"
  fi
  if [[ "$has_google_service_account" == "1" ]]; then
    printf " GOOGLE_SERVICE_ACCOUNT_JSON_FILE=/path/to/service-account.json"
  fi
  printf " ./scripts/setup-firebase-secrets.sh\n"
  echo "     MOONLY_BIND_ALL_SECRETS=1 ./scripts/deploy-firebase.sh"
}

run_required_check() {
  local label="$1"
  local log_name="$2"
  shift 2

  local log_file
  log_file="$(log_path "$log_name")"

  if "$@" >"$log_file" 2>&1; then
    ok "$label"
    if grep -q "^\[WARN\]" "$log_file"; then
      warn "$label produced warnings; see $log_file"
    fi
  else
    block "$label failed; see $log_file"
  fi
}

run_optional_check() {
  local label="$1"
  local log_name="$2"
  shift 2

  local log_file
  log_file="$(log_path "$log_name")"

  if "$@" >"$log_file" 2>&1; then
    ok "$label"
    if grep -q "^\[WARN\]" "$log_file"; then
      warn "$label produced warnings; see $log_file"
    fi
  else
    warn "$label failed; see $log_file"
  fi
}

file_is_stale_against_sources() {
  local marker="$1"
  [[ -f "$marker" ]] || return 1

  find \
    Assets/Scripts \
    Assets/Plugins \
    Assets/Editor \
    Assets/GameData \
    Assets/Resources \
    Assets/GamerFrameWork \
    Packages \
    ProjectSettings \
    -type f \
    -newer "$marker" \
    -print \
    -quit 2>/dev/null | grep -q .
}

check_local_secret_env() {
  local missing=()

  for key in APPLE_SHARED_SECRET GOOGLE_PACKAGE_NAME; do
    if [[ -z "${!key:-}" ]]; then
      missing+=("$key")
    fi
  done

  if [[ -z "${GOOGLE_SERVICE_ACCOUNT_JSON:-}" && -z "${GOOGLE_SERVICE_ACCOUNT_JSON_FILE:-}" ]]; then
    missing+=("GOOGLE_SERVICE_ACCOUNT_JSON or GOOGLE_SERVICE_ACCOUNT_JSON_FILE")
  fi

  if [[ "${#missing[@]}" -eq 0 ]]; then
    ok "local IAP secret environment variables are present for setup-firebase-secrets.sh"
  else
    warn "local IAP secret env is incomplete: ${missing[*]}"
  fi
}

check_real_iap_receipt_input() {
  local receipt="${IAP_RECEIPT:-${REAL_IAP_RECEIPT:-}}"
  local store="${IAP_STORE:-AppleAppStore}"
  local product_id="${IAP_PRODUCT_ID:-fari.pro.monthly}"

  if [[ -z "$receipt" ]]; then
    block "real sandbox IAP receipt input is missing; set IAP_RECEIPT or REAL_IAP_RECEIPT"
    return 1
  fi

  if [[ -z "$product_id" ]]; then
    block "IAP_PRODUCT_ID is required for real sandbox IAP receipt verification"
    return 1
  fi

  case "$store" in
    AppleAppStore|GooglePlay)
      ok "real IAP receipt input is present for $store / $product_id"
      return 0
      ;;
    *)
      block "IAP_STORE must be AppleAppStore or GooglePlay for real receipt verification: $store"
      return 1
      ;;
  esac
}

check_android_signing_env() {
  local missing=()

  if ! grep -q "androidUseCustomKeystore: 1" ProjectSettings/ProjectSettings.asset; then
    ok "Android custom keystore is not required by ProjectSettings"
    return
  fi

  [[ -n "${ANDROID_KEYSTORE_PASS:-}" ]] || missing+=("ANDROID_KEYSTORE_PASS")
  [[ -n "${ANDROID_KEYALIAS_PASS:-}" ]] || missing+=("ANDROID_KEYALIAS_PASS")

  if [[ "${#missing[@]}" -eq 0 ]]; then
    ok "Android custom keystore signing password env is present"
    run_required_check "Android custom keystore password and alias validation" "android-keystore" scripts/check-android-keystore.sh
  else
    block "Android release rebuild needs signing password env: ${missing[*]}"
  fi
}

check_selected_public_config() {
  local args=("functions/scripts/set-public-config.js" "--dry-run" "--project" "$PROJECT_ID")

  if truthy "$REQUIRE_REAL_SOCIAL_LINKS"; then
    args+=("--require-real-social-links")
  fi

  args+=("$PUBLIC_CONFIG_PATH")
  run_required_check "selected public app config release validation: $PUBLIC_CONFIG_PATH" "public-config-selected" node "${args[@]}"
}

cd "$ROOT_DIR" || exit 1

echo "Moonly release blockers check"
echo "Project: $ROOT_DIR"
echo "Firebase project: $PROJECT_ID"
echo "Logs: $LOG_DIR"
if [[ -n "${MOONLY_RELEASE_ENV_FILE:-}" ]]; then
  echo "Release env file: $MOONLY_RELEASE_ENV_FILE"
fi
echo

if [[ -f Temp/UnityLockfile ]] && command -v lsof >/dev/null 2>&1 && lsof Temp/UnityLockfile >/dev/null 2>&1; then
  block "Unity currently has this project open; close Unity before batchmode export/build"
elif [[ -f Temp/UnityLockfile ]]; then
  warn "Temp/UnityLockfile exists; confirm Unity is closed before building"
else
  ok "Unity project is not locked"
fi

if scripts/configure-google-play-games.sh --check >/tmp/moonly_release_google_play_games_app_id.log 2>&1; then
  ok "Google Play Games APP_ID is configured"
elif truthy "${REQUIRE_GOOGLE_PLAY_GAMES:-0}"; then
  block "Google Play Games APP_ID is required but still missing/placeholder; run GOOGLE_PLAY_GAMES_APP_ID=<id> ./scripts/configure-google-play-games.sh; see /tmp/moonly_release_google_play_games_app_id.log"
else
  warn "Google Play Games APP_ID is missing/placeholder; current Google login does not require Play Games, set REQUIRE_GOOGLE_PLAY_GAMES=1 to make this a blocker"
fi

check_local_secret_env
echo

run_required_check "IAP product ID consistency" "iap-products" scripts/check-iap-products.sh
run_required_check "public config dry-run validation" "public-config-dry-run" node functions/scripts/set-public-config.js --dry-run functions/public-config.example.json
if truthy "${RUN_PUBLIC_CONFIG_UPDATE:-0}"; then
  check_selected_public_config
else
  warn "public app config update is not selected; set RUN_PUBLIC_CONFIG_UPDATE=1 PUBLIC_CONFIG_PATH=functions/public-config.live.json REQUIRE_REAL_SOCIAL_LINKS=1 when publishing real social/IAP display config"
fi
run_required_check "Android source configuration" "android-source" scripts/check-android-config.sh
run_required_check "Firebase SDK version consistency" "firebase-sdk-versions" bash scripts/check-firebase-sdk-versions.sh
run_required_check "iOS export validator self-test" "ios-export-self-test" scripts/check-ios-export.sh --self-test
echo

if [[ -d "$IOS_EXPORT_PATH" ]]; then
  run_required_check "existing iOS export validation: $IOS_EXPORT_PATH" "ios-export-existing" scripts/check-ios-export.sh "$IOS_EXPORT_PATH"

  ios_marker="$IOS_EXPORT_PATH/.export-stamp"
  if [[ ! -f "$ios_marker" ]]; then
    ios_marker="$IOS_EXPORT_PATH/Unity-iPhone.xcodeproj/project.pbxproj"
  fi
  if file_is_stale_against_sources "$ios_marker"; then
    block "existing iOS export is older than current source/config; rebuild with CLEAN_IOS_EXPORT=1 ./scripts/build-ios-xcode.sh"
  else
    ok "existing iOS export is not older than checked source/config paths"
  fi
else
  block "iOS export missing: $IOS_EXPORT_PATH; run CLEAN_IOS_EXPORT=1 ./scripts/build-ios-xcode.sh"
fi

if [[ -f "$ANDROID_APK_PATH" ]]; then
  run_required_check "existing Android APK validation: $ANDROID_APK_PATH" "android-apk-existing" scripts/check-android-config.sh --apk "$ANDROID_APK_PATH"

  android_marker="$ANDROID_APK_PATH.build-stamp"
  if [[ ! -f "$android_marker" ]]; then
    android_marker="$ANDROID_APK_PATH"
  fi
  if file_is_stale_against_sources "$android_marker"; then
    ANDROID_REBUILD_NEEDED=1
    block "existing Android APK is older than current source/config; rebuild with CLEAN_ANDROID_BUILD=1 ./scripts/build-android-apk.sh"
  else
    ok "existing Android APK is not older than checked source/config paths"
  fi
else
  ANDROID_REBUILD_NEEDED=1
  block "Android APK missing: $ANDROID_APK_PATH; run CLEAN_ANDROID_BUILD=1 ./scripts/build-android-apk.sh"
fi
if [[ "$ANDROID_REBUILD_NEEDED" == "1" ]]; then
  check_android_signing_env
fi
echo

if [[ "${CHECK_FIREBASE_SECRETS:-0}" == "1" ]]; then
  run_required_check "remote Firebase Functions secrets" "firebase-secrets" scripts/check-firebase-secrets.sh
  collect_remote_missing_secrets
else
  block "remote Firebase Functions secrets not verified; run CHECK_FIREBASE_SECRETS=1 ./scripts/check-release-blockers.sh"
fi

if [[ "${CHECK_FUNCTIONS_SMOKE:-0}" == "1" ]]; then
  run_required_check "strict authenticated Functions smoke tests" "functions-smoke" env REQUIRE_AI_TTS_LIVE=1 scripts/smoke-functions-auth.sh
else
  block "strict authenticated Functions smoke tests not run; run CHECK_FUNCTIONS_SMOKE=1 ./scripts/check-release-blockers.sh"
fi

if [[ "${CHECK_IAP_FAKE_SMOKE:-0}" == "1" ]]; then
  run_optional_check "fake submitIapReceipt smoke test" "iap-fake-smoke" env IAP_SMOKE_MODE=fake scripts/smoke-submit-iap-receipt.sh
else
  warn "fake submitIapReceipt smoke test skipped; run CHECK_IAP_FAKE_SMOKE=1 ./scripts/check-release-blockers.sh for backend path smoke"
fi

if [[ "${CHECK_IAP_REAL_RECEIPT:-0}" == "1" || "${IAP_SMOKE_MODE:-}" == "real" || -n "${IAP_RECEIPT:-${REAL_IAP_RECEIPT:-}}" ]]; then
  if check_real_iap_receipt_input; then
    run_required_check "real sandbox IAP receipt verification" "iap-real-receipt" env IAP_SMOKE_MODE=real scripts/smoke-submit-iap-receipt.sh
  fi
else
  block "real sandbox IAP receipt has not been verified; set IAP_RECEIPT and run CHECK_IAP_REAL_RECEIPT=1 ./scripts/check-release-blockers.sh"
fi
echo

cat <<'EOF'
Release unblock commands:
  1. Create and fill the private local release env file:
     ./scripts/init-release-env.sh

     Required values:
       ANDROID_KEYSTORE_PASS
       ANDROID_KEYALIAS_PASS
       APPLE_SHARED_SECRET
       GOOGLE_SERVICE_ACCOUNT_JSON_FILE or GOOGLE_SERVICE_ACCOUNT_JSON
       IAP_RECEIPT or REAL_IAP_RECEIPT
       PUBLIC_CONFIG_PATH=functions/public-config.live.json with real social links if updating public config

  2. Recommended one-command continuation after the env file is filled:
     RELEASE_ENV_FILE=scripts/release.env ./scripts/finish-release.sh

  3. Manual artifact rebuild path:
     CLEAN_IOS_EXPORT=1 ./scripts/build-ios-xcode.sh
     ANDROID_KEYSTORE_PASS=<keystore_password> ANDROID_KEYALIAS_PASS=<alias_password> CLEAN_ANDROID_BUILD=1 ./scripts/build-android-apk.sh

  4. Configure Google Play Games only if Play Games services are enabled:
     GOOGLE_PLAY_GAMES_APP_ID=<numeric_app_id> ./scripts/configure-google-play-games.sh

  5. Publish real social links and public IAP display config:
     ./scripts/init-public-config.sh
     # edit functions/public-config.live.json with real account/community URLs
     RUN_PUBLIC_CONFIG_UPDATE=1 PUBLIC_CONFIG_PATH=functions/public-config.live.json REQUIRE_REAL_SOCIAL_LINKS=1 ./scripts/prepare-release.sh

  6. Manual IAP secret setup path:
EOF
print_iap_secret_unblock_command
cat <<'EOF'

  7. Manual online release verification:
     CHECK_FIREBASE_SECRETS=1 CHECK_FUNCTIONS_SMOKE=1 CHECK_IAP_FAKE_SMOKE=1 CHECK_IAP_REAL_RECEIPT=1 RUN_PUBLIC_CONFIG_UPDATE=1 PUBLIC_CONFIG_PATH=functions/public-config.live.json IAP_RECEIPT=<sandbox_receipt> ./scripts/check-release-blockers.sh
EOF
echo

echo "Summary: $BLOCKERS blocker(s), $WARNINGS warning(s), $OKS ok check(s)"
if [[ "$BLOCKERS" -gt 0 && "${ALLOW_RELEASE_BLOCKERS:-0}" != "1" ]]; then
  exit 1
fi

exit 0
