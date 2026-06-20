#!/usr/bin/env bash
set -u

PROJECT_ID="${FIREBASE_PROJECT:-fari-app-b2fd2}"
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
FAILURES=0
WARNINGS=0
UNITY_REGISTRY="${UNITY_REGISTRY:-https://packages.unity.cn}"

if [[ -n "${MOONLY_PROXY:-}" ]]; then
  export HTTPS_PROXY="$MOONLY_PROXY"
  export HTTP_PROXY="$MOONLY_PROXY"
fi

if [[ -n "${MOONLY_ALL_PROXY:-}" ]]; then
  export ALL_PROXY="$MOONLY_ALL_PROXY"
fi

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

has_file() {
  local path="$1"
  if [[ -f "$ROOT_DIR/$path" ]]; then
    ok "$path exists"
  else
    fail "$path missing"
  fi
}

json_has_package() {
  local file="$1"
  local package="$2"
  python3 - "$ROOT_DIR/$file" "$package" <<'PY'
import json
import sys

path, package = sys.argv[1], sys.argv[2]
try:
    with open(path, "r", encoding="utf-8-sig") as fh:
        data = json.load(fh)
except Exception:
    sys.exit(2)

deps = data.get("dependencies", {})
sys.exit(0 if package in deps else 1)
PY
}

grep_file() {
  local pattern="$1"
  local file="$2"
  grep -q -- "$pattern" "$ROOT_DIR/$file"
}

project_settings_group_has_define() {
  local group="$1"
  local define="$2"
  grep -E "^[[:space:]]+$group: .*(^|;)$define(;|$)" "$ROOT_DIR/ProjectSettings/ProjectSettings.asset" >/dev/null 2>&1
}

registry_has_version() {
  local registry="$1"
  local package="$2"
  local version="$3"
  node - "$registry" "$package" "$version" <<'NODE'
const https = require("https");

const registry = process.argv[2].replace(/\/$/, "");
const packageName = process.argv[3];
const version = process.argv[4];
const url = `${registry}/${packageName}`;

const request = https.get(url, { timeout: 15000 }, response => {
  let body = "";
  response.setEncoding("utf8");
  response.on("data", chunk => {
    body += chunk;
  });
  response.on("end", () => {
    if (response.statusCode < 200 || response.statusCode >= 300) {
      console.error(`HTTP ${response.statusCode}`);
      process.exit(2);
      return;
    }

    try {
      const data = JSON.parse(body);
      process.exit(data.versions && data.versions[version] ? 0 : 1);
    } catch (error) {
      console.error(error.message);
      process.exit(2);
    }
  });
});

request.on("timeout", () => {
  request.destroy(new Error("request timeout"));
});
request.on("error", error => {
  console.error(error.message);
  process.exit(2);
});
NODE
}

cd "$ROOT_DIR" || exit 1

echo "Moonly local readiness check"
echo "Project: $ROOT_DIR"
echo

has_file "Packages/manifest.json"
has_file "Packages/packages-lock.json"
has_file "functions/index.js"
has_file "firestore.rules"
has_file "scripts/deploy-firebase.sh"
has_file "scripts/deploy-iap-functions.sh"
has_file "scripts/setup-firebase-secrets.sh"
has_file "scripts/check-firebase-secrets.sh"
has_file "scripts/resolve-unity-packages.sh"
has_file "scripts/check-firebase-network.sh"
has_file "scripts/check-ios-export.sh"
has_file "scripts/build-ios-xcode.sh"
has_file "scripts/check-android-config.sh"
has_file "scripts/check-android-keystore.sh"
has_file "scripts/build-android-apk.sh"
has_file "scripts/configure-google-play-games.sh"
has_file "scripts/check-iap-products.sh"
has_file "scripts/check-release-blockers.sh"
has_file "scripts/prepare-release.sh"
has_file "scripts/finish-release.sh"
has_file "scripts/check-release-env.sh"
has_file "scripts/release.env.example"
has_file "scripts/smoke-submit-iap-receipt.sh"
has_file "scripts/smoke-functions-auth.sh"
has_file "Assets/Scripts/GameManager/AppReadinessDiagnostics.cs"
has_file "Assets/Editor/AppReadinessDiagnosticsMenu.cs"
has_file "Assets/Editor/AppPackageResolverMenu.cs"
has_file "Assets/Editor/FariIOSContactsPostprocessor.cs"
has_file "Assets/Scripts/GameManager/AppNotificationScheduler.cs"
has_file "Assets/Scripts/GameManager/NotificationSettingsManager.cs"
has_file "Assets/Plugins/Android/AndroidManifest.xml"
echo

if python3 -m json.tool Packages/manifest.json >/dev/null 2>&1; then
  ok "Packages/manifest.json is valid JSON"
else
  fail "Packages/manifest.json is invalid JSON"
fi

for package in com.unity.purchasing com.unity.mobile.notifications; do
  if json_has_package "Packages/manifest.json" "$package"; then
    ok "manifest contains $package"
  else
    fail "manifest missing $package"
  fi

  if json_has_package "Packages/packages-lock.json" "$package"; then
    ok "packages-lock contains $package"
  else
    warn "packages-lock missing $package; open/refresh Unity Package Manager to resolve it"
  fi
done

for group in Android iPhone Standalone; do
  if project_settings_group_has_define "$group" "UNITY_PURCHASING"; then
    ok "ProjectSettings $group defines UNITY_PURCHASING"
  else
    fail "ProjectSettings $group missing UNITY_PURCHASING scripting define"
  fi
done

if grep_file "android.permission.POST_NOTIFICATIONS" "Assets/Plugins/Android/AndroidManifest.xml"; then
  ok "AndroidManifest declares POST_NOTIFICATIONS"
else
  fail "AndroidManifest missing POST_NOTIFICATIONS for Android 13+ notifications"
fi
echo

if [[ "${CHECK_REGISTRY:-0}" == "1" ]]; then
  if command -v node >/dev/null 2>&1; then
    if registry_has_version "$UNITY_REGISTRY" "com.unity.purchasing" "4.12.2"; then
      ok "$UNITY_REGISTRY has com.unity.purchasing@4.12.2"
    else
      fail "$UNITY_REGISTRY cannot confirm com.unity.purchasing@4.12.2"
    fi

    if registry_has_version "$UNITY_REGISTRY" "com.unity.mobile.notifications" "2.3.2"; then
      ok "$UNITY_REGISTRY has com.unity.mobile.notifications@2.3.2"
    else
      fail "$UNITY_REGISTRY cannot confirm com.unity.mobile.notifications@2.3.2"
    fi
  else
    fail "node not found; cannot check Unity registry"
  fi
else
  warn "Unity registry version check skipped; run CHECK_REGISTRY=1 scripts/check-local-readiness.sh to verify package versions"
fi
echo

if [[ -f Temp/UnityLockfile ]] && command -v lsof >/dev/null 2>&1 && lsof Temp/UnityLockfile >/dev/null 2>&1; then
  warn "Unity currently has this project open; batchmode package resolve cannot run in parallel"
  lsof Temp/UnityLockfile 2>/dev/null | sed 's/^/  /'
elif [[ -f Temp/UnityLockfile ]]; then
  warn "Temp/UnityLockfile exists; remove it only after confirming Unity is closed"
else
  ok "Unity project is not locked"
  ok "batchmode package resolver can be run: scripts/resolve-unity-packages.sh"
fi

UNITY_BIN="/Users/kittenhao/Unity/UnityEditor/2022.3.34f1c1/Unity.app/Contents/MacOS/Unity"
if [[ -x "$UNITY_BIN" ]]; then
  ok "Unity executable found: $("$UNITY_BIN" -version 2>/dev/null | head -n 1)"
else
  warn "Unity executable not found at expected path: $UNITY_BIN"
fi
echo

for export_name in membershipStatus readinessStatus publicConfig submitFeedback adminPublicConfigUpdate adminFeedbackList adminFeedbackUpdate deleteMyAccountData submitIapReceipt aiChat aiChatStream ttsSynthesize; do
  if grep_file "exports\\.$export_name" "functions/index.js"; then
    ok "functions exports $export_name"
  else
    fail "functions missing export $export_name"
  fi
done

if npm --prefix functions run lint >/tmp/moonly_readiness_functions_lint.log 2>&1; then
  ok "functions syntax check passed"
else
  fail "functions syntax check failed; see /tmp/moonly_readiness_functions_lint.log"
fi
echo

if grep_file "readinessStatus" "scripts/deploy-firebase.sh"; then
  ok "deploy script includes readinessStatus"
else
  fail "deploy script does not include readinessStatus"
fi

if bash -n scripts/deploy-firebase.sh >/tmp/moonly_readiness_deploy_firebase_syntax.log 2>&1; then
  ok "Firebase deploy script syntax check passed"
else
  fail "Firebase deploy script syntax check failed; see /tmp/moonly_readiness_deploy_firebase_syntax.log"
fi

if grep_file "submitIapReceipt" "scripts/deploy-iap-functions.sh"; then
  ok "IAP deploy script includes submitIapReceipt"
else
  fail "IAP deploy script does not include submitIapReceipt"
fi

if bash -n scripts/deploy-iap-functions.sh >/tmp/moonly_readiness_deploy_iap_syntax.log 2>&1; then
  ok "IAP deploy script syntax check passed"
else
  fail "IAP deploy script syntax check failed; see /tmp/moonly_readiness_deploy_iap_syntax.log"
fi

if [[ -x scripts/check-iap-products.sh ]]; then
  if scripts/check-iap-products.sh >/tmp/moonly_readiness_iap_products.log 2>&1; then
    ok "IAP product config consistency check passed"
  else
    fail "IAP product config consistency check failed; see /tmp/moonly_readiness_iap_products.log"
  fi
else
  fail "scripts/check-iap-products.sh is not executable"
fi

if [[ -x scripts/check-release-blockers.sh ]]; then
  if bash -n scripts/check-release-blockers.sh >/tmp/moonly_readiness_release_blockers_syntax.log 2>&1; then
    ok "release blockers script syntax check passed"
  else
    fail "release blockers script syntax check failed; see /tmp/moonly_readiness_release_blockers_syntax.log"
  fi
else
  fail "scripts/check-release-blockers.sh is not executable"
fi

if [[ -x scripts/prepare-release.sh ]]; then
  if bash -n scripts/prepare-release.sh >/tmp/moonly_readiness_prepare_release_syntax.log 2>&1; then
    ok "release preparation script syntax check passed"
  else
    fail "release preparation script syntax check failed; see /tmp/moonly_readiness_prepare_release_syntax.log"
  fi
else
  fail "scripts/prepare-release.sh is not executable"
fi

if [[ -x scripts/finish-release.sh ]]; then
  if bash -n scripts/finish-release.sh >/tmp/moonly_readiness_finish_release_syntax.log 2>&1; then
    ok "final release continuation script syntax check passed"
  else
    fail "final release continuation script syntax check failed; see /tmp/moonly_readiness_finish_release_syntax.log"
  fi
else
  fail "scripts/finish-release.sh is not executable"
fi

if [[ -x scripts/check-release-env.sh ]]; then
  if bash -n scripts/check-release-env.sh >/tmp/moonly_readiness_check_release_env_syntax.log 2>&1; then
    ok "release env check script syntax check passed"
  else
    fail "release env check script syntax check failed; see /tmp/moonly_readiness_check_release_env_syntax.log"
  fi
else
  fail "scripts/check-release-env.sh is not executable"
fi

if [[ -x scripts/check-android-keystore.sh ]]; then
  if bash -n scripts/check-android-keystore.sh >/tmp/moonly_readiness_android_keystore_syntax.log 2>&1; then
    ok "Android keystore check script syntax check passed"
  else
    fail "Android keystore check script syntax check failed; see /tmp/moonly_readiness_android_keystore_syntax.log"
  fi
else
  fail "scripts/check-android-keystore.sh is not executable"
fi

if grep_file "RELEASE_ENV_FILE" "scripts/prepare-release.sh" \
  && grep_file "MOONLY_RELEASE_ENV_FILE" "scripts/prepare-release.sh"; then
  ok "release preparation script can load a local release env file"
else
  fail "release preparation script missing release env file loading support"
fi

if grep_file "RELEASE_ENV_FILE" "scripts/check-release-blockers.sh" \
  && grep_file "MOONLY_RELEASE_ENV_FILE" "scripts/check-release-blockers.sh"; then
  ok "release blockers script can load a local release env file"
else
  fail "release blockers script missing release env file loading support"
fi

if grep_file "RUN_IAP_SECRET_SETUP=1" "scripts/finish-release.sh" \
  && grep_file "RUN_DEPLOY=1" "scripts/finish-release.sh" \
  && grep_file "RUN_BUILDS=1" "scripts/finish-release.sh" \
  && grep_file "CHECK_IAP_REAL_RECEIPT=1" "scripts/finish-release.sh" \
  && grep_file "--no-env-file" "scripts/finish-release.sh" \
  && grep_file "scripts/prepare-release.sh" "scripts/finish-release.sh"; then
  ok "final release continuation script wires secret setup, deploy, builds, release gate, and env-free mode"
else
  fail "final release continuation script missing required final release steps"
fi

if env \
  ANDROID_KEYSTORE_PASS=readiness_keystore \
  ANDROID_KEYALIAS_PASS=readiness_alias \
  APPLE_SHARED_SECRET=readiness_apple_shared_secret \
  GOOGLE_PACKAGE_NAME=com.canchentechnology.fari \
  GOOGLE_SERVICE_ACCOUNT_JSON='{"type":"service_account","client_email":"readiness@fari-app-b2fd2.iam.gserviceaccount.com","private_key":"-----BEGIN PRIVATE KEY-----\nREADINESS\n-----END PRIVATE KEY-----\n"}' \
  IAP_RECEIPT=readiness_receipt \
  IAP_STORE=AppleAppStore \
  IAP_PRODUCT_ID=moonly.pro.monthly \
  SKIP_ANDROID_KEYSTORE_VALIDATION=1 \
  ./scripts/check-release-env.sh --no-env-file >/tmp/moonly_readiness_release_env_check.log 2>&1; then
  ok "release env check script validates complete release inputs without printing secrets"
else
  fail "release env check script dry-run validation failed; see /tmp/moonly_readiness_release_env_check.log"
fi

if node functions/scripts/set-public-config.js --dry-run functions/public-config.example.json >/tmp/moonly_readiness_public_config.log 2>&1; then
  ok "public config validation dry-run passed"
else
  fail "public config validation dry-run failed; see /tmp/moonly_readiness_public_config.log"
fi

if grep_file "CommandLineBuild.BuildIOSProject" "scripts/build-ios-xcode.sh" \
  && grep_file "-buildTarget iOS" "scripts/build-ios-xcode.sh" \
  && grep_file "check-ios-export.sh" "scripts/build-ios-xcode.sh"; then
  ok "iOS Xcode export script builds and validates exported project"
else
  fail "iOS Xcode export script is missing build target, build method, or validation step"
fi

if grep_file "RELEASE_ENV_FILE" "scripts/build-ios-xcode.sh" \
  && grep_file "MOONLY_RELEASE_ENV_FILE" "scripts/build-ios-xcode.sh"; then
  ok "iOS Xcode export script can load local release env file"
else
  fail "iOS Xcode export script missing release env file support"
fi

if grep_file "CommandLineBuild.BuildAndroidApk" "scripts/build-android-apk.sh" \
  && grep_file "-buildTarget Android" "scripts/build-android-apk.sh" \
  && grep_file "check-android-config.sh" "scripts/build-android-apk.sh" \
  && grep_file "check-android-keystore.sh" "scripts/build-android-apk.sh"; then
  ok "Android APK build script builds and validates APK"
else
  fail "Android APK build script is missing build target, build method, keystore check, or validation step"
fi

if grep_file "RELEASE_ENV_FILE" "scripts/build-android-apk.sh" \
  && grep_file "MOONLY_RELEASE_ENV_FILE" "scripts/build-android-apk.sh"; then
  ok "Android APK build script can load local release env file"
else
  fail "Android APK build script missing release env file support"
fi

if grep_file "ANDROID_KEYSTORE_PASS" "scripts/build-android-apk.sh" \
  && grep_file "ANDROID_KEYALIAS_PASS" "scripts/build-android-apk.sh" \
  && grep_file "androidUseCustomKeystore" "scripts/build-android-apk.sh"; then
  ok "Android APK build script validates custom keystore signing passwords before batchmode build"
else
  fail "Android APK build script missing custom keystore signing preflight"
fi

if grep_file "GOOGLE_PLAY_GAMES_APP_ID" "scripts/configure-google-play-games.sh" \
  && grep_file "com.google.android.gms.games.APP_ID" "scripts/configure-google-play-games.sh" \
  && grep_file "DRY_RUN" "scripts/configure-google-play-games.sh" \
  && grep_file "CHECK_ONLY" "scripts/configure-google-play-games.sh"; then
  ok "Google Play Games APP_ID configure script is available with dry-run/check support"
else
  fail "Google Play Games APP_ID configure script is missing required update or validation logic"
fi

if grep_file "validate_google_play_games_app_id" "scripts/prepare-release.sh" \
  && grep_file "validate_iap_secret_inputs" "scripts/prepare-release.sh" \
  && grep_file "validate_real_iap_receipt_inputs" "scripts/prepare-release.sh"; then
  ok "release preparation script validates external release inputs before running steps"
else
  fail "release preparation script missing external input validation"
fi

if grep_file "WAIT_FOR_UNITY_CLOSE" "scripts/prepare-release.sh" \
  && grep_file "UNITY_WAIT_TIMEOUT_SECONDS" "scripts/prepare-release.sh" \
  && grep_file "wait_for_unity_close" "scripts/prepare-release.sh"; then
  ok "release preparation script can wait for Unity to close before batchmode builds"
else
  fail "release preparation script missing wait-for-Unity-close support"
fi

if grep_file "configure-google-play-games.sh --check" "scripts/check-release-blockers.sh" \
  && grep_file "check_real_iap_receipt_input" "scripts/check-release-blockers.sh"; then
  ok "release blockers script reuses Google Play Games check and validates real IAP receipt input"
else
  fail "release blockers script missing Google Play Games check reuse or real IAP receipt input validation"
fi

if grep_file "REQUIRE_GOOGLE_PLAY_GAMES" "scripts/check-release-blockers.sh" \
  && grep_file "current Google login does not require Play Games" "scripts/check-release-blockers.sh"; then
  ok "release blockers script treats Google Play Games APP_ID as optional unless explicitly required"
else
  fail "release blockers script should not require Google Play Games APP_ID by default"
fi

if grep_file "check_android_signing_env" "scripts/check-release-blockers.sh" \
  && grep_file "ANDROID_KEYSTORE_PASS" "scripts/check-release-blockers.sh" \
  && grep_file "ANDROID_KEYALIAS_PASS" "scripts/check-release-blockers.sh" \
  && grep_file "check-android-keystore.sh" "scripts/check-release-blockers.sh"; then
  ok "release blockers script reports and validates Android signing env when APK rebuild is needed"
else
  fail "release blockers script missing Android signing env release check"
fi

if grep_file "AndroidKeystoreName" "scripts/check-android-keystore.sh" \
  && grep_file "AndroidKeyaliasName" "scripts/check-android-keystore.sh" \
  && grep_file "ANDROID_KEYSTORE_PASS" "scripts/check-android-keystore.sh" \
  && grep_file "ANDROID_KEYALIAS_PASS" "scripts/check-android-keystore.sh" \
  && grep_file "-importkeystore" "scripts/check-android-keystore.sh"; then
  ok "Android keystore check validates configured keystore, alias, store password, and key password"
else
  fail "Android keystore check missing required signing validation logic"
fi

if grep_file "collect_remote_missing_secrets" "scripts/check-release-blockers.sh" \
  && grep_file "print_iap_secret_unblock_command" "scripts/check-release-blockers.sh"; then
  ok "release blockers script prints IAP secret unblock commands from actual remote missing secrets"
else
  fail "release blockers script missing dynamic IAP secret unblock command output"
fi

if grep_file "APPLE_SHARED_SECRET" "scripts/release.env.example" \
  && grep_file "GOOGLE_SERVICE_ACCOUNT_JSON_FILE" "scripts/release.env.example" \
  && grep_file "IAP_RECEIPT" "scripts/release.env.example"; then
  ok "release env template documents IAP and release inputs"
else
  fail "release env template missing required release inputs"
fi

if grep_file "scripts/release.env" ".gitignore" \
  && grep_file "scripts/release.local.env" ".gitignore" \
  && grep_file "!scripts/release.env.example" ".gitignore"; then
  ok "gitignore protects local release env files while keeping the example template"
else
  fail "gitignore missing local release env protections"
fi

if [[ -x scripts/check-ios-export.sh ]]; then
  if scripts/check-ios-export.sh --self-test >/tmp/moonly_readiness_ios_export_selftest.log 2>&1; then
    ok "iOS export validation script self-test passed"
  else
    fail "iOS export validation script self-test failed; see /tmp/moonly_readiness_ios_export_selftest.log"
  fi
else
  fail "scripts/check-ios-export.sh is not executable"
fi

if grep_file "Copy iOS Xcode Export Command" "Assets/Editor/AppReadinessDiagnosticsMenu.cs"; then
  ok "Editor menu can copy iOS Xcode export command"
else
  fail "Editor menu missing iOS Xcode export command"
fi

if grep_file "Copy Android APK Build Command" "Assets/Editor/AppReadinessDiagnosticsMenu.cs"; then
  ok "Editor menu can copy Android APK build command"
else
  fail "Editor menu missing Android APK build command"
fi

if grep_file "Copy Android Keystore Check Command" "Assets/Editor/AppReadinessDiagnosticsMenu.cs"; then
  ok "Editor menu can copy Android keystore check command"
else
  fail "Editor menu missing Android keystore check command"
fi

if grep_file "Copy Release Blockers Command" "Assets/Editor/AppReadinessDiagnosticsMenu.cs"; then
  ok "Editor menu can copy release blockers command"
else
  fail "Editor menu missing release blockers command"
fi

if grep_file "Copy Release Blockers Env Command" "Assets/Editor/AppReadinessDiagnosticsMenu.cs"; then
  ok "Editor menu can copy release blockers env command"
else
  fail "Editor menu missing release blockers env command"
fi

if grep_file "Copy Prepare Release Command" "Assets/Editor/AppReadinessDiagnosticsMenu.cs"; then
  ok "Editor menu can copy prepare release command"
else
  fail "Editor menu missing prepare release command"
fi

if grep_file "Copy Prepare Release Env Command" "Assets/Editor/AppReadinessDiagnosticsMenu.cs"; then
  ok "Editor menu can copy prepare release env command"
else
  fail "Editor menu missing prepare release env command"
fi

if grep_file "Copy Check Release Env Command" "Assets/Editor/AppReadinessDiagnosticsMenu.cs"; then
  ok "Editor menu can copy release env check command"
else
  fail "Editor menu missing release env check command"
fi

if grep_file "Copy Finish Release Env Command" "Assets/Editor/AppReadinessDiagnosticsMenu.cs"; then
  ok "Editor menu can copy final release continuation command"
else
  fail "Editor menu missing final release continuation command"
fi

if [[ -x scripts/check-android-config.sh ]]; then
  if scripts/check-android-config.sh >/tmp/moonly_readiness_android_config.log 2>&1; then
    ok "Android source config check passed"
    if grep -q "^\[WARN\]" /tmp/moonly_readiness_android_config.log; then
      warn "Android source config has non-blocking warnings; see /tmp/moonly_readiness_android_config.log"
    fi
  else
    fail "Android source config check failed; see /tmp/moonly_readiness_android_config.log"
  fi
else
  fail "scripts/check-android-config.sh is not executable"
fi

if grep_file "functions:secrets:set" "scripts/setup-firebase-secrets.sh"; then
  ok "secret setup script can write Firebase Functions secrets"
else
  fail "secret setup script is missing functions:secrets:set"
fi

if grep_file "MOONLY_SECRET_NAMES" "scripts/setup-firebase-secrets.sh" \
  && grep_file "REQUESTED_SECRET_NAMES" "scripts/setup-firebase-secrets.sh"; then
  ok "secret setup script supports partial secret updates"
else
  fail "secret setup script missing partial secret update support"
fi

if grep_file "functions:secrets:access" "scripts/check-firebase-secrets.sh"; then
  ok "secret check script can verify remote Firebase Functions secrets"
else
  fail "secret check script is missing functions:secrets:access"
fi

if grep_file "ScheduleDiagnosticNotification" "Assets/Scripts/GameManager/AppNotificationScheduler.cs" \
  && grep_file "BuildScheduledDebugSummary" "Assets/Scripts/GameManager/AppNotificationScheduler.cs"; then
  ok "notification scheduler exposes diagnostic scheduling and scheduled-state summary"
else
  fail "notification scheduler missing diagnostic scheduling or scheduled-state summary"
fi

if grep_file "PresentationOptions" "Assets/Scripts/GameManager/AppNotificationScheduler.cs" \
  && grep_file "RepeatInterval.*Daily" "Assets/Scripts/GameManager/AppNotificationScheduler.cs"; then
  ok "notification scheduler configures alert presentation and daily repeat scheduling"
else
  fail "notification scheduler missing alert presentation or daily repeat scheduling"
fi

if grep_file "Schedule Test Notification" "Assets/Editor/AppReadinessDiagnosticsMenu.cs"; then
  ok "Editor menu can schedule a test notification"
else
  fail "Editor menu missing test notification action"
fi

if grep_file "SaveNotificationSettings" "Assets/Scripts/UI/NotionUI.cs" \
  && grep_file "LoadNotificationSettings" "Assets/Scripts/UI/NotionUI.cs"; then
  ok "notification settings UI syncs settings with Firestore"
else
  fail "notification settings UI is not wired to Firestore settings sync"
fi

if grep_file "SendPasswordResetEmail" "Assets/Scripts/Platform/FireBase/FirebaseAuthManager.cs" \
  && grep_file "SubmitPasswordReset" "Assets/Scripts/UI/LoginUI.cs"; then
  ok "email login UI supports Firebase password reset emails"
else
  fail "email login UI is missing Firebase password reset support"
fi

if grep_file "SignInWithGameCenter" "Assets/Scripts/Platform/FireBase/FirebaseAuthManager.cs" \
  && grep_file "GameCenterAuthProvider.GetCredentialAsync" "Assets/Scripts/Platform/FireBase/FirebaseAuthManager.cs" \
  && grep_file "OnGameCenterSignInButtonClick" "Assets/Scripts/UI/LoginUI.cs" \
  && grep_file "IsGameCenterAuthProviderResolved" "Assets/Scripts/GameManager/AppReadinessDiagnostics.cs"; then
  ok "Game Center login is wired to Firebase Auth provider"
else
  fail "Game Center login is not wired to Firebase Auth provider"
fi

if grep_file "AddGameCenter" "Assets/Editor/FariIOSContactsPostprocessor.cs" \
  && grep_file "AddSignInWithApple" "Assets/Editor/FariIOSContactsPostprocessor.cs" \
  && grep_file "AddInAppPurchase" "Assets/Editor/FariIOSContactsPostprocessor.cs" \
  && grep_file "NSContactsUsageDescription" "Assets/Editor/FariIOSContactsPostprocessor.cs"; then
  ok "iOS export postprocessor configures Game Center, Apple Sign-In, IAP, and contacts usage"
else
  fail "iOS export postprocessor is missing one or more required capabilities"
fi

if grep_file "NotifyFriendRequestCount" "Assets/Scripts/UI/FriendUI.cs" \
  && grep_file "NotifyRelationshipInviteCount" "Assets/Scripts/UI/FriendUI.cs" \
  && grep_file "NotifyFriendDailyOracleCount" "Assets/Scripts/UI/FriendUI.cs" \
  && grep_file "NotifyTomorrowHookCount" "Assets/Scripts/UI/TodayOracleUI.cs" \
  && grep_file "ScheduleTomorrowHookReminder" "Assets/Scripts/UI/DialogUI.cs"; then
  ok "friend, relationship, daily-oracle feed, and Tomorrow Hook notification triggers are wired"
else
  fail "one or more notification trigger call sites are missing"
fi

if [[ "${CHECK_IOS_EXPORT:-0}" == "1" ]]; then
  ios_export_path="${IOS_EXPORT_PATH:-Builds/iOS}"
  if scripts/check-ios-export.sh "$ios_export_path" >/tmp/moonly_readiness_ios_export.log 2>&1; then
    ok "existing iOS export validation passed: $ios_export_path"
  else
    fail "existing iOS export validation failed for $ios_export_path; see /tmp/moonly_readiness_ios_export.log"
  fi
fi

if [[ "${CHECK_IOS_BUILD:-0}" == "1" ]]; then
  if scripts/build-ios-xcode.sh >/tmp/moonly_readiness_ios_build.log 2>&1; then
    ok "iOS Xcode export build and validation passed"
  else
    fail "iOS Xcode export build failed; see /tmp/moonly_readiness_ios_build.log"
  fi
fi

if [[ "${CHECK_ANDROID_APK:-0}" == "1" ]]; then
  android_apk_path="${ANDROID_APK_PATH:-Builds/Android/MoonlyApp.apk}"
  if scripts/check-android-config.sh --apk "$android_apk_path" >/tmp/moonly_readiness_android_apk.log 2>&1; then
    ok "existing Android APK validation passed: $android_apk_path"
  else
    fail "existing Android APK validation failed for $android_apk_path; see /tmp/moonly_readiness_android_apk.log"
  fi
fi

if [[ "${CHECK_ANDROID_BUILD:-0}" == "1" ]]; then
  if scripts/build-android-apk.sh >/tmp/moonly_readiness_android_build.log 2>&1; then
    ok "Android APK build and validation passed"
  else
    fail "Android APK build failed; see /tmp/moonly_readiness_android_build.log"
  fi
fi

if [[ "${CHECK_SECRETS_ENV:-0}" == "1" ]]; then
  missing_secret_env=()
  for key in DEEPSEEK_API_KEY VOLC_TTS_API_KEY APPLE_SHARED_SECRET GOOGLE_PACKAGE_NAME GOOGLE_SERVICE_ACCOUNT_JSON; do
    if [[ "$key" == "GOOGLE_SERVICE_ACCOUNT_JSON" && -n "${GOOGLE_SERVICE_ACCOUNT_JSON_FILE:-}" ]]; then
      continue
    fi
    if [[ -z "${!key:-}" ]]; then
      missing_secret_env+=("$key")
    fi
  done

  if [[ "${#missing_secret_env[@]}" -eq 0 ]]; then
    ok "required secret environment variables are present"
  else
    warn "missing secret environment variables: ${missing_secret_env[*]}"
  fi

  if [[ -n "${GOOGLE_SERVICE_ACCOUNT_JSON:-}" || -n "${GOOGLE_SERVICE_ACCOUNT_JSON_FILE:-}" ]]; then
    if node - "$GOOGLE_SERVICE_ACCOUNT_JSON_FILE" <<'NODE' >/dev/null 2>&1
const fs = require("fs");
const fromEnv = process.env.GOOGLE_SERVICE_ACCOUNT_JSON || "";
const fromFile = process.argv[2] || "";
const raw = fromEnv || fs.readFileSync(fromFile, "utf8");
const data = JSON.parse(raw);
if (data.type !== "service_account") throw new Error("type must be service_account");
if (!data.client_email) throw new Error("client_email missing");
if (!data.private_key) throw new Error("private_key missing");
NODE
    then
      ok "GOOGLE_SERVICE_ACCOUNT_JSON is valid service account JSON"
    else
      fail "GOOGLE_SERVICE_ACCOUNT_JSON is not valid Google service account JSON"
    fi
  fi
else
  warn "secret env check skipped; run CHECK_SECRETS_ENV=1 scripts/check-local-readiness.sh to verify required secret variables"
fi

if [[ "${CHECK_FIREBASE_SECRETS:-0}" == "1" ]]; then
  if [[ -x scripts/check-firebase-secrets.sh ]]; then
    if scripts/check-firebase-secrets.sh >/tmp/moonly_readiness_firebase_secrets.log 2>&1; then
      ok "Firebase Functions remote secrets check passed"
    else
      fail "Firebase Functions remote secrets check failed; see /tmp/moonly_readiness_firebase_secrets.log"
    fi
  else
    fail "scripts/check-firebase-secrets.sh is not executable"
  fi
else
  warn "remote Firebase secret check skipped; run CHECK_FIREBASE_SECRETS=1 scripts/check-local-readiness.sh to verify deployed secrets"
fi

if [[ "${CHECK_FIREBASE:-0}" == "1" ]]; then
  if command -v firebase >/dev/null 2>&1; then
    if firebase login:list --json >/tmp/moonly_readiness_firebase_login.json 2>/tmp/moonly_readiness_firebase.err \
      && node -e 'const fs=require("fs"); const data=JSON.parse(fs.readFileSync("/tmp/moonly_readiness_firebase_login.json","utf8")); process.exit(Array.isArray(data.result)&&data.result.length>0?0:1);' >/dev/null 2>&1; then
      ok "Firebase CLI has an active login"
    else
      fail "Firebase CLI has no active login; run firebase login --reauth"
    fi

    if firebase use --json >/tmp/moonly_readiness_firebase_use.json 2>>/tmp/moonly_readiness_firebase.err \
      && node - "$PROJECT_ID" <<'NODE' >/dev/null 2>&1
const fs = require("fs");
const expectedProject = process.argv[2];
const data = JSON.parse(fs.readFileSync("/tmp/moonly_readiness_firebase_use.json", "utf8"));
process.exit(data.result === expectedProject ? 0 : 1);
NODE
    then
      ok "Firebase active project is $PROJECT_ID"
    else
      fail "Firebase active project is not $PROJECT_ID; run firebase use $PROJECT_ID"
    fi
  else
    fail "firebase CLI not found"
  fi
else
  warn "Firebase CLI login/project check skipped; run CHECK_FIREBASE=1 scripts/check-local-readiness.sh for auth verification"
fi

if [[ "${CHECK_FIREBASE_NETWORK:-0}" == "1" ]]; then
  if [[ -x scripts/check-firebase-network.sh ]]; then
    if scripts/check-firebase-network.sh >/tmp/moonly_readiness_firebase_network.log 2>&1; then
      ok "Firebase network check passed"
    else
      fail "Firebase network check failed; see /tmp/moonly_readiness_firebase_network.log"
    fi
  else
    fail "scripts/check-firebase-network.sh is not executable"
  fi
else
  warn "Firebase network check skipped; run CHECK_FIREBASE_NETWORK=1 scripts/check-local-readiness.sh to verify Editor/backend connectivity"
fi

if [[ "${CHECK_FIREBASE_FUNCTIONS:-0}" == "1" ]]; then
  if command -v firebase >/dev/null 2>&1; then
    if firebase functions:list --project "$PROJECT_ID" --json >/tmp/moonly_readiness_functions_list.json 2>/tmp/moonly_readiness_functions_list.err; then
      for function_name in membershipStatus readinessStatus publicConfig submitFeedback adminPublicConfigUpdate adminFeedbackList adminFeedbackUpdate deleteMyAccountData; do
        if node - "$function_name" <<'NODE' >/dev/null 2>&1
const fs = require("fs");
const functionName = process.argv[2];
const data = JSON.parse(fs.readFileSync("/tmp/moonly_readiness_functions_list.json", "utf8"));
const list = Array.isArray(data.result) ? data.result : [];
process.exit(list.some(item => item.id === functionName || item.name === functionName) ? 0 : 1);
NODE
        then
          ok "Firebase Function deployed: $function_name"
        else
          fail "Firebase Function missing: $function_name"
        fi
      done

      for function_name in aiChat aiChatStream ttsSynthesize submitIapReceipt paymentWebhook; do
        if node - "$function_name" <<'NODE' >/dev/null 2>&1
const fs = require("fs");
const functionName = process.argv[2];
const data = JSON.parse(fs.readFileSync("/tmp/moonly_readiness_functions_list.json", "utf8"));
const list = Array.isArray(data.result) ? data.result : [];
process.exit(list.some(item => item.id === functionName || item.name === functionName) ? 0 : 1);
NODE
        then
          ok "Firebase Function deployed: $function_name"
        else
          warn "Firebase Function not deployed yet: $function_name"
        fi
      done
    else
      if [[ "${CHECK_FIREBASE_FUNCTIONS_STRICT:-0}" == "1" ]]; then
        fail "Firebase Functions list failed; see /tmp/moonly_readiness_functions_list.err"
      elif [[ "${CHECK_FUNCTIONS_SMOKE:-0}" == "1" || "${CHECK_IAP_SMOKE:-0}" == "1" ]]; then
        warn "Firebase Functions list failed; requested smoke tests will verify callable endpoints instead. Set CHECK_FIREBASE_FUNCTIONS_STRICT=1 to fail on list errors."
      else
        fail "Firebase Functions list failed; see /tmp/moonly_readiness_functions_list.err. Re-run with CHECK_FUNCTIONS_SMOKE=1 and/or CHECK_IAP_SMOKE=1 to verify endpoints when functions:list is flaky."
      fi
    fi
  else
    fail "firebase CLI not found"
  fi
else
  warn "Firebase Functions deployment check skipped; run CHECK_FIREBASE_FUNCTIONS=1 scripts/check-local-readiness.sh to verify online Functions"
fi

if [[ "${CHECK_IAP_SMOKE:-0}" == "1" ]]; then
  if [[ -x scripts/smoke-submit-iap-receipt.sh ]]; then
    if scripts/smoke-submit-iap-receipt.sh >/tmp/moonly_readiness_iap_smoke.log 2>&1; then
      ok "submitIapReceipt authenticated smoke test passed"
    else
      fail "submitIapReceipt authenticated smoke test failed; see /tmp/moonly_readiness_iap_smoke.log"
    fi
  else
    fail "scripts/smoke-submit-iap-receipt.sh is not executable"
  fi
else
  warn "submitIapReceipt smoke test skipped; run CHECK_IAP_SMOKE=1 scripts/check-local-readiness.sh to verify authenticated receipt submission"
fi

if [[ "${CHECK_FUNCTIONS_SMOKE:-0}" == "1" ]]; then
  if [[ -x scripts/smoke-functions-auth.sh ]]; then
    if REQUIRE_AI_TTS_LIVE="${CHECK_FUNCTIONS_SMOKE_STRICT:-0}" scripts/smoke-functions-auth.sh >/tmp/moonly_readiness_functions_smoke.log 2>&1; then
      ok "Firebase Functions authenticated smoke tests passed"
    else
      fail "Firebase Functions authenticated smoke tests failed; see /tmp/moonly_readiness_functions_smoke.log"
    fi
  else
    fail "scripts/smoke-functions-auth.sh is not executable"
  fi
else
  warn "Firebase Functions smoke tests skipped; run CHECK_FUNCTIONS_SMOKE=1 scripts/check-local-readiness.sh to verify client-facing Functions"
fi

if [[ "${CHECK_BUILD:-0}" == "1" ]]; then
  if dotnet build Assembly-CSharp.csproj >/tmp/moonly_readiness_build.log 2>&1 \
    && dotnet build Assembly-CSharp-Editor.csproj >>/tmp/moonly_readiness_build.log 2>&1; then
    ok "C# build checks passed"
  else
    fail "C# build checks failed; see /tmp/moonly_readiness_build.log"
  fi

  iap_defines="$(sed -n 's:.*<DefineConstants>\(.*\)</DefineConstants>.*:\1:p' Assembly-CSharp.csproj | head -n 1)"
  if [[ -n "$iap_defines" ]]; then
    case ";$iap_defines;" in
      *";UNITY_PURCHASING;"*) ;;
      *) iap_defines="$iap_defines;UNITY_PURCHASING" ;;
    esac
    iap_defines_escaped="${iap_defines//;/%3B}"
    if dotnet build Assembly-CSharp.csproj -p:DefineConstants="$iap_defines_escaped" >/tmp/moonly_readiness_iap_build.log 2>&1; then
      ok "Unity IAP bridge compile check passed"
    else
      fail "Unity IAP bridge compile check failed; see /tmp/moonly_readiness_iap_build.log"
    fi
  else
    warn "could not inspect Assembly-CSharp.csproj DefineConstants for IAP bridge compile check"
  fi
else
  warn "C# build check skipped; run CHECK_BUILD=1 scripts/check-local-readiness.sh for compile verification"
fi

echo
echo "Summary: $FAILURES failure(s), $WARNINGS warning(s)"
if [[ "$FAILURES" -gt 0 ]]; then
  exit 1
fi

exit 0
