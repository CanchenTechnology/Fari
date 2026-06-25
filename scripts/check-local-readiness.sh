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
has_file "scripts/check-firebase-sdk-versions.sh"
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
has_file "scripts/init-release-env.sh"
has_file "scripts/init-public-config.sh"
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

for export_name in membershipStatus readinessStatus publicConfig submitFeedback adminPublicConfigUpdate adminFeedbackList adminFeedbackUpdate deleteMyAccountData submitIapReceipt aiChat aiChatStream processStaleDialogueReplyJobs ttsSynthesize; do
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

if [[ -x scripts/init-release-env.sh ]]; then
  if bash -n scripts/init-release-env.sh >/tmp/moonly_readiness_init_release_env_syntax.log 2>&1; then
    ok "release env init script syntax check passed"
  else
    fail "release env init script syntax check failed; see /tmp/moonly_readiness_init_release_env_syntax.log"
  fi
else
  fail "scripts/init-release-env.sh is not executable"
fi

if [[ -x scripts/init-public-config.sh ]]; then
  if bash -n scripts/init-public-config.sh >/tmp/moonly_readiness_init_public_config_syntax.log 2>&1; then
    ok "public config init script syntax check passed"
  else
    fail "public config init script syntax check failed; see /tmp/moonly_readiness_init_public_config_syntax.log"
  fi
else
  fail "scripts/init-public-config.sh is not executable"
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
  && grep_file "check-release-env.sh" "scripts/finish-release.sh" \
  && grep_file "init-release-env.sh" "scripts/finish-release.sh" \
  && grep_file "--no-env-file" "scripts/finish-release.sh" \
  && grep_file "scripts/prepare-release.sh" "scripts/finish-release.sh"; then
  ok "final release continuation script validates inputs, wires secret setup, deploy, builds, release gate, and env-free mode"
else
  fail "final release continuation script missing input validation or required final release steps"
fi

if grep_file "scripts/release.env.example" "scripts/init-release-env.sh" \
  && grep_file "chmod 600" "scripts/init-release-env.sh" \
  && grep_file "init-public-config.sh" "scripts/init-release-env.sh" \
  && grep_file "PUBLIC_CONFIG_PATH=functions/public-config.live.json" "scripts/init-release-env.sh" \
  && grep_file "REQUIRE_REAL_SOCIAL_LINKS=1" "scripts/init-release-env.sh" \
  && grep_file "check-release-env.sh" "scripts/init-release-env.sh" \
  && grep_file "check-android-keystore.sh" "scripts/init-release-env.sh" \
  && grep_file "prepare-release.sh" "scripts/init-release-env.sh" \
  && grep_file "finish-release.sh" "scripts/init-release-env.sh"; then
  ok "release env init script creates private local env and prints the validation / release path"
else
  fail "release env init script missing private env creation or release command guidance"
fi

if grep_file "functions/public-config.example.json" "scripts/init-public-config.sh" \
  && grep_file "functions/public-config.live.json" "scripts/init-public-config.sh" \
  && grep_file "--require-real-social-links" "scripts/init-public-config.sh" \
  && grep_file "RUN_PUBLIC_CONFIG_UPDATE=1" "scripts/init-public-config.sh"; then
  ok "public config init script creates live config and prints validation / release commands"
else
  fail "public config init script missing live config creation or release command guidance"
fi

if grep_file "init-release-env.sh" "scripts/check-release-env.sh" \
  && grep_file "init-release-env.sh" "scripts/prepare-release.sh" \
  && grep_file "init-release-env.sh" "scripts/check-release-blockers.sh" \
  && grep_file "init-release-env.sh" "scripts/build-ios-xcode.sh" \
  && grep_file "init-release-env.sh" "scripts/build-android-apk.sh" \
  && grep_file "init-release-env.sh" "scripts/check-android-keystore.sh"; then
  ok "release env missing-file guidance points to the initializer across release scripts"
else
  fail "release env missing-file guidance is not consistently wired to the initializer"
fi

if env \
  ANDROID_KEYSTORE_PASS=readiness_keystore \
  ANDROID_KEYALIAS_PASS=readiness_alias \
  APPLE_SHARED_SECRET=readiness_apple_shared_secret \
  GOOGLE_PACKAGE_NAME=com.canchentechnology.fari \
  GOOGLE_SERVICE_ACCOUNT_JSON='{"type":"service_account","client_email":"readiness@fari-app-b2fd2.iam.gserviceaccount.com","private_key":"-----BEGIN PRIVATE KEY-----\nREADINESS\n-----END PRIVATE KEY-----\n"}' \
  IAP_RECEIPT=readiness_real_receipt_payload_for_format_check \
  IAP_STORE=AppleAppStore \
  IAP_PRODUCT_ID=fari.pro.monthly \
  SKIP_ANDROID_KEYSTORE_VALIDATION=1 \
  ./scripts/check-release-env.sh --no-env-file >/tmp/moonly_readiness_release_env_check.log 2>&1; then
  ok "release env check script validates complete release inputs without printing secrets"
else
  fail "release env check script dry-run validation failed; see /tmp/moonly_readiness_release_env_check.log"
fi

if grep_file "ProjectSettings Android bundle id" "scripts/check-release-env.sh" \
  && grep_file "GOOGLE_PACKAGE_NAME matches" "scripts/check-release-env.sh"; then
  ok "release env check verifies Google package name against ProjectSettings Android bundle id"
else
  fail "release env check missing Google package name and Android bundle id consistency validation"
fi

if grep_file "APPLE_SHARED_SECRET format looks usable" "scripts/check-release-env.sh" \
  && grep_file "real sandbox IAP receipt input format looks usable" "scripts/check-release-env.sh" \
  && grep_file "moonly-smoke-receipt" "scripts/check-release-env.sh" \
  && grep_file "BEGIN PRIVATE KEY" "scripts/check-release-env.sh"; then
  ok "release env check validates Apple secret, service account PEM, and real receipt shape"
else
  fail "release env check missing Apple secret, service account PEM, or real receipt shape validation"
fi

if node functions/scripts/set-public-config.js --dry-run functions/public-config.example.json >/tmp/moonly_readiness_public_config.log 2>&1; then
  ok "public config validation dry-run passed"
else
  fail "public config validation dry-run failed; see /tmp/moonly_readiness_public_config.log"
fi

if grep_file "--require-real-social-links" "functions/scripts/set-public-config.js" \
  && grep_file "isPlaceholderSocialLink" "functions/scripts/set-public-config.js"; then
  ok "public config setter can reject placeholder social links for release"
else
  fail "public config setter missing real social link validation"
fi

if grep_file "RUN_PUBLIC_CONFIG_UPDATE" "scripts/prepare-release.sh" \
  && grep_file "validate_public_config_inputs" "scripts/prepare-release.sh" \
  && grep_file "functions/scripts/set-public-config.js" "scripts/prepare-release.sh" \
  && grep_file "selected public app config release validation" "scripts/check-release-blockers.sh" \
  && grep_file "init-public-config.sh" "scripts/check-release-blockers.sh" \
  && grep_file "RUN_PUBLIC_CONFIG_UPDATE=1 PUBLIC_CONFIG_PATH=functions/public-config.live.json" "scripts/check-release-blockers.sh" \
  && grep_file "Copy Init Public Config Command" "Assets/Editor/AppReadinessDiagnosticsMenu.cs" \
  && grep_file "Copy Public Config Release Command" "Assets/Editor/AppReadinessDiagnosticsMenu.cs" \
  && grep_file "RUN_PUBLIC_CONFIG_UPDATE" "scripts/finish-release.sh" \
  && grep_file "validate_public_config" "scripts/check-release-env.sh" \
  && grep_file "PUBLIC_CONFIG_PATH" "scripts/release.env.example" \
  && grep_file "REQUIRE_REAL_SOCIAL_LINKS" "scripts/release.env.example"; then
  ok "release flow can validate and publish real public social/IAP config"
else
  fail "release flow missing public social/IAP config validation or update wiring"
fi

if grep_file "CommandLineBuild.BuildIOSProject" "scripts/build-ios-xcode.sh" \
  && grep_file "-buildTarget iOS" "scripts/build-ios-xcode.sh" \
  && grep_file "check-ios-export.sh" "scripts/build-ios-xcode.sh"; then
  ok "iOS Xcode export script builds and validates exported project"
else
  fail "iOS Xcode export script is missing build target, build method, or validation step"
fi

if grep_file ".export-stamp" "scripts/build-ios-xcode.sh" \
  && grep_file ".export-stamp" "scripts/check-release-blockers.sh"; then
  ok "iOS Xcode export script writes a completion stamp and release gate uses it for freshness"
else
  fail "iOS Xcode export freshness stamp is not wired through build and release gate scripts"
fi

if node <<'NODE' >/tmp/moonly_readiness_window_config.log 2>&1
const fs = require("fs");
const path = require("path");

const configPath = "Assets/Resources/WindowConfig.asset";
const lines = fs.readFileSync(configPath, "utf8").split(/\r?\n/);
const entries = [];
for (let i = 0; i < lines.length; i += 1) {
  const nameMatch = lines[i].match(/^  - name: (.+)$/);
  if (!nameMatch) continue;
  const pathMatch = (lines[i + 1] || "").match(/^    path: (.+)$/);
  entries.push({ name: nameMatch[1], path: pathMatch ? pathMatch[1] : "" });
}

const byName = new Map();
const duplicates = [];
const missing = [];
for (const entry of entries) {
  if (byName.has(entry.name)) duplicates.push(entry.name);
  byName.set(entry.name, entry);

  if (!entry.path) {
    missing.push(`${entry.name}: <empty>`);
  } else if (entry.path.startsWith("Assets/")) {
    if (!fs.existsSync(`${entry.path}.prefab`)) missing.push(`${entry.name}: ${entry.path}`);
  } else if (!resourcePrefabExists(entry.path)) {
    missing.push(`${entry.name}: ${entry.path}`);
  }
}

const myWindows = [
  "AccountUI",
  "FeedbackUI",
  "FollowusUI",
  "HistoryUI",
  "MemoryManagementUI",
  "MyUI",
  "NotionUI",
  "PersonalProfileUI",
  "UnlockProUI",
];
const misplacedMyWindows = myWindows.filter((name) => {
  const entry = byName.get(name);
  return !entry || !entry.path.startsWith("Assets/GameData/UI/Main/My/");
});

if (duplicates.length > 0 || missing.length > 0 || misplacedMyWindows.length > 0) {
  if (duplicates.length > 0) console.error(`duplicate window names: ${[...new Set(duplicates)].join(", ")}`);
  if (missing.length > 0) console.error(`missing prefab paths: ${missing.join(", ")}`);
  if (misplacedMyWindows.length > 0) console.error(`my windows not under Main/My: ${misplacedMyWindows.join(", ")}`);
  process.exit(1);
}

function resourcePrefabExists(resourcePath) {
  const suffix = path.join("Resources", `${resourcePath}.prefab`);
  const stack = ["Assets"];
  while (stack.length > 0) {
    const current = stack.pop();
    for (const child of fs.readdirSync(current, { withFileTypes: true })) {
      const childPath = path.join(current, child.name);
      if (child.isDirectory()) {
        stack.push(childPath);
      } else if (child.isFile() && childPath.endsWith(suffix)) {
        return true;
      }
    }
  }
  return false;
}
NODE
then
  ok "WindowConfig has unique window names and valid prefab paths"
else
  fail "WindowConfig has duplicate names, missing prefab paths, or misplaced My windows; see /tmp/moonly_readiness_window_config.log"
fi

if grep_file "Unhandled user patch event" "Assets/Scripts/HotFix/PatchOperation.cs" \
  && grep_file "Unhandled patch event" "Assets/Scripts/UI/PatchWindow.cs" \
  && grep_file "TotalDownloadCount <= 0" "Assets/Scripts/UI/PatchWindow.cs" \
  && ! grep_file "NotImplementedException" "Assets/Scripts/HotFix/PatchOperation.cs" \
  && ! grep_file "NotImplementedException" "Assets/Scripts/UI/PatchWindow.cs"; then
  ok "patch update flow handles unknown events and zero-count progress without crashing"
else
  fail "patch update flow still has crash-prone unhandled event or progress handling"
fi

if grep_file "ResolveFallbackResultViews" "Assets/Scripts/UI/UserSearchUI.cs" \
  && grep_file "fallbackResultRoots" "Assets/Scripts/UI/UserSearchUI.cs" \
  && grep_file "IsRecommendationItem" "Assets/Scripts/UI/UserSearchUI.cs" \
  && grep_file "SetActive(hasResult)" "Assets/Scripts/UI/UserSearchUI.cs"; then
  ok "user search result fallback views resolve at runtime when LoopListView is unavailable"
else
  fail "user search fallback result views are not resolved safely"
fi

if node <<'NODE' >/tmp/moonly_readiness_ui_bindings.log 2>&1
const fs = require("fs");
const path = require("path");

const uiTypes = new Set([
  "Button",
  "Text",
  "Image",
  "InputField",
  "ScrollRect",
  "Toggle",
  "ToggleGroup",
  "Transform",
  "RectTransform",
]);
const componentPrefabs = [
  ["Assets/Scripts/UI/MyUIComponent.cs", "Assets/GameData/UI/Main/My/MyUI.prefab"],
  ["Assets/Scripts/UI/PersonalProfileUIComponent.cs", "Assets/GameData/UI/Main/My/PersonalProfileUI.prefab"],
  ["Assets/Scripts/UI/MemoryManagementUIComponent.cs", "Assets/GameData/UI/Main/My/MemoryManagementUI.prefab"],
  ["Assets/Scripts/UI/AccountUIComponent.cs", "Assets/GameData/UI/Main/My/AccountUI.prefab"],
  ["Assets/Scripts/UI/NotionUIComponent.cs", "Assets/GameData/UI/Main/My/NotionUI.prefab"],
  ["Assets/Scripts/UI/FeedbackUIComponent.cs", "Assets/GameData/UI/Main/My/FeedbackUI.prefab"],
  ["Assets/Scripts/UI/FollowusUIComponent.cs", "Assets/GameData/UI/Main/My/FollowusUI.prefab"],
  ["Assets/Scripts/UI/UnlockProUIComponent.cs", "Assets/GameData/UI/Main/My/UnlockProUI.prefab"],
  ["Assets/Scripts/UI/HistoryUIComponent.cs", "Assets/GameData/UI/Main/My/HistoryUI.prefab"],
  ["Assets/Scripts/UI/DivinationRecordUIComponent.cs", "Assets/GameData/UI/Main/TodayDivination/DivinationRecordUI.prefab"],
];

const failures = [];
for (const [scriptPath, prefabPath] of componentPrefabs) {
  const script = fs.readFileSync(scriptPath, "utf8");
  const meta = fs.readFileSync(`${scriptPath}.meta`, "utf8");
  const guid = meta.match(/guid:\s*([a-fA-F0-9]+)/)?.[1];
  if (!guid) {
    failures.push(`${scriptPath}: missing script guid`);
    continue;
  }

  const fields = [...script.matchAll(/^\s*public\s+([A-Za-z0-9_<>.\[\]]+)\s+([A-Za-z0-9_]+)\s*;/gm)]
    .filter((match) => uiTypes.has(match[1].replace(/\[\]$/, "")))
    .map((match) => match[2]);
  const prefab = fs.readFileSync(prefabPath, "utf8");
  const blocks = prefab
    .split(/\n(?=--- !u!)/g)
    .filter((block) => block.includes("MonoBehaviour:") && block.includes(`guid: ${guid}`));

  if (blocks.length === 0) {
    failures.push(`${prefabPath}: missing component ${path.basename(scriptPath)}`);
    continue;
  }

  for (let blockIndex = 0; blockIndex < blocks.length; blockIndex += 1) {
    for (const field of fields) {
      const value = blocks[blockIndex].match(new RegExp(`^\\s*${field}:\\s*\\{fileID:\\s*([^,}]+)`, "m"))?.[1];
      if (!value) {
        failures.push(`${prefabPath}: component ${path.basename(scriptPath)} block ${blockIndex + 1} missing field ${field}`);
      } else if (value.trim() === "0") {
        failures.push(`${prefabPath}: component ${path.basename(scriptPath)} block ${blockIndex + 1} has unbound field ${field}`);
      }
    }
  }
}

if (failures.length > 0) {
  console.error(failures.join("\n"));
  process.exit(1);
}
NODE
then
  ok "My/History UIComponent prefab bindings are present and non-zero"
else
  fail "My/History UIComponent prefab binding check failed; see /tmp/moonly_readiness_ui_bindings.log"
fi

if ! grep_file "viewButton" "Assets/Scripts/UI/HistoryUIComponent.cs" \
  && ! grep_file "OnviewButtonClick" "Assets/Scripts/UI/HistoryUI.cs" \
  && ! grep_file "viewButton: {fileID: 0}" "Assets/GameData/UI/Main/My/HistoryUI.prefab"; then
  ok "History UI no longer carries stale unbound viewButton wiring"
else
  fail "History UI still contains stale viewButton wiring"
fi

if node <<'NODE' >/tmp/moonly_readiness_my_feature_guards.log 2>&1
const fs = require("fs");
const myUI = fs.readFileSync("Assets/Scripts/UI/MyUI.cs", "utf8");
const myUIPrefab = fs.readFileSync("Assets/GameData/UI/Main/My/MyUI.prefab", "utf8");
const history = fs.readFileSync("Assets/Scripts/UI/HistoryUI.cs", "utf8");
const feedback = fs.readFileSync("Assets/Scripts/UI/FeedbackUI.cs", "utf8");
const firestore = fs.readFileSync("Assets/Scripts/Platform/FireBase/FirestoreManager.cs", "utf8");
const divinationRecordStore = fs.readFileSync("Assets/Scripts/Platform/FireBase/DivinationRecordFirestore.cs", "utf8");

const myDashboardUsesReadingQuota = /tatTodayCardValueText\.text = stats\.GetReadingDisplay\(_isPro\)/.test(myUI)
  && /m_text: "\\u4ECA\\u65E5\\u5360\\u535C"/.test(myUIPrefab);

const historySafeStore = /DivinationHistoryCacheService/.test(history)
  && /cache\.Refresh\(false/.test(history)
  && /历史服务暂不可用/.test(history)
  && /CacheRecord\(record\);/.test(divinationRecordStore)
  && /DivinationHistoryCacheService\.Instance\.UpsertRecord\(record\);/.test(divinationRecordStore)
  && /public List<DivinationRecordData> LoadCachedRecords\(\)[\s\S]*?return LoadCachedRecordsLocal\(\);/.test(divinationRecordStore)
  && /QueuePendingRecordDelete\(readingId\);/.test(divinationRecordStore)
  && /SyncLocalCacheToCloud\(\)/.test(divinationRecordStore)
  && !/占卜历史不再保存到本地/.test(divinationRecordStore);

const feedbackMigratesLocalPending = /MergeLocalPendingFeedbackEntries\(\)/.test(feedback)
  && /LoadCachedFeedbackEntriesForUid\("local", localEntries, clearTarget: true\)/.test(feedback)
  && /SaveFeedbackEntriesToCacheForUid\("local", remainingLocal\)/.test(feedback);

const feedbackHandlesCloudEmptyAndFailure = /if \(entries != null\)[\s\S]*?_feedbackEntries\.Clear\(\)/.test(feedback)
  && /public void LoadFeedback\(Action<List<CloudFeedbackEntry>> onComplete, int limit = 30\)[\s\S]*?加载反馈失败[\s\S]*?onComplete\?\.Invoke\(null\)/.test(firestore);

const feedbackComposerSeparatedFromSearch = /GetCommunityPublishText\(\)/.test(feedback)
  && /IsCommunityPublishInput\(TMP_InputField input\)/.test(feedback)
  && /ClearCommunityPublishInput\(\)/.test(feedback)
  && /public void OnSearchInputInputChange\(string text\)[\s\S]*?IsCommunityPublishInput\(uiComponent\.SearchInputInputField\)[\s\S]*?return;[\s\S]*?_searchText = text/.test(feedback)
  && /string content = GetCommunityPublishText\(\)/.test(feedback);

if (!myDashboardUsesReadingQuota || !historySafeStore || !feedbackMigratesLocalPending || !feedbackHandlesCloudEmptyAndFailure || !feedbackComposerSeparatedFromSearch) {
  console.error(JSON.stringify({
    myDashboardUsesReadingQuota,
    historySafeStore,
    feedbackMigratesLocalPending,
    feedbackHandlesCloudEmptyAndFailure,
    feedbackComposerSeparatedFromSearch,
  }, null, 2));
  process.exit(1);
}
NODE
then
  ok "My feature guards cover dashboard quota, history fallback, and feedback pending-cache/composer behavior"
else
  fail "My feature guard check failed; see /tmp/moonly_readiness_my_feature_guards.log"
fi

if node <<'NODE' >/tmp/moonly_readiness_my_module_requirements.log 2>&1
const fs = require("fs");

const read = (path) => fs.readFileSync(path, "utf8");
const files = {
  my: read("Assets/Scripts/UI/MyUI.cs"),
  history: read("Assets/Scripts/UI/HistoryUI.cs"),
  historyDetail: read("Assets/Scripts/UI/DivinationHistoryUI.cs"),
  detail: read("Assets/Scripts/UI/DivinationRecordUI.cs"),
  profile: read("Assets/Scripts/UI/PersonalProfileUI.cs"),
  registration: read("Assets/Scripts/UI/RegistrationFlowUtility.cs"),
  avatarUpload: read("Assets/Scripts/Platform/FireBase/AvatarUploadManager.cs"),
  memory: read("Assets/Scripts/UI/MemoryManagementUI.cs"),
  memoryList: read("Assets/Scripts/UI/MemoryManageListUI.cs"),
  memoryStore: read("Assets/Scripts/GameManager/MemoryUiStore.cs"),
  memoryDetail: read("Assets/Scripts/UI/MemoryDetailUI.cs"),
  dialogUi: read("Assets/Scripts/UI/DialogUI.cs"),
  account: read("Assets/Scripts/UI/AccountUI.cs"),
  notion: read("Assets/Scripts/UI/NotionUI.cs"),
  notifManager: read("Assets/Scripts/GameManager/NotificationSettingsManager.cs"),
  notifScheduler: read("Assets/Scripts/GameManager/AppNotificationScheduler.cs"),
  notifUnread: read("Assets/Scripts/GameManager/NotificationUnreadState.cs"),
  dialogSystem: read("Assets/Scripts/Dialog/Data/DialogSystem.cs"),
  addFriend: read("Assets/Scripts/UI/AddFriendUI.cs"),
  contactsInvite: read("Assets/Scripts/UI/ContactsInviteUI.cs"),
  facebookInvite: read("Assets/Scripts/UI/FacebookInviteUI.cs"),
  userSearch: read("Assets/Scripts/UI/UserSearchUI.cs"),
  createFriendSuccess: read("Assets/Scripts/UI/CreateFriendSuccessUI.cs"),
  feedback: read("Assets/Scripts/UI/FeedbackUI.cs"),
  follow: read("Assets/Scripts/UI/FollowusUI.cs"),
  unlock: read("Assets/Scripts/UI/UnlockProUI.cs"),
  iapBridge: read("Assets/Scripts/Platform/IAP/UnityIapPurchaseBridge.cs"),
  firestore: read("Assets/Scripts/Platform/FireBase/FirestoreManager.cs"),
};

const checks = {
  myHome:
    /RefreshDashboard\(\)/.test(files.my)
    && /GetReadingDisplay\(_isPro\)/.test(files.my)
    && /GetDialogDisplay\(_isPro\)/.test(files.my)
    && /LoadLatestDivination\(requestId\)/.test(files.my)
    && /OnLatestRecordEntryClick\(\)[\s\S]*?PopUpWindow<DivinationRecordUI>/.test(files.my)
    && /PopUpWindow<HistoryUI>/.test(files.my)
    && /PopUpWindow<PersonalProfileUI>/.test(files.my)
    && /PopUpWindow<MemoryManageUI>/.test(files.my)
    && /PopUpWindow<UnlockProUI>/.test(files.my)
    && /PopUpWindow<AccountUI>/.test(files.my)
    && /PopUpWindow<NotionUI>/.test(files.my)
    && /PopUpWindow<FeedbackUI>/.test(files.my)
    && /PopUpWindow<FollowusUI>/.test(files.my),
  history:
    /DivinationHistoryCacheService/.test(files.history)
    && /cache\.Refresh\(false/.test(files.history)
    && /ValidateSelectedRecord\(\)/.test(files.history)
    && /SelectedRecord = record/.test(files.history)
    && /PopUpWindow<DivinationHistoryUI>/.test(files.history)
    && /ShowEmptyState/.test(files.history),
  divinationDetail:
    /ActivateReadingFromRecord\(_currentRecord, DivinationPhase\.FollowUp\)/.test(files.detail)
    && /DivinationRecordFirestore\.SaveRecordLocal\(_currentRecord\)/.test(files.detail)
    && /firestore\.SaveRecord\(_currentRecord/.test(files.detail)
    && /bool localSaved = DivinationRecordFirestore\.SaveRecordLocal\(_currentRecord\)/.test(files.historyDetail)
    && /localSaved \? "已保存到本地，云端稍后同步" : "保存失败，请稍后再试"/.test(files.historyDetail)
    && /记录已保存到本地缓存/.test(files.historyDetail)
    && /OnDeleteRecordButtonClick\(\)[\s\S]*?DeleteRecord/.test(files.detail)
    && /BuildShareText\(\)/.test(files.detail)
    && /EnsureSaveToDiaryButtonInteractable\(\)/.test(files.detail),
  personalProfile:
    /BioInputInputField/.test(files.profile)
    && /PickAndUploadAvatar/.test(files.profile)
    && /TryNormalizeBirthday/.test(files.profile)
    && /TryNormalizeBirthTime/.test(files.profile)
    && /SetProfileBio\(bio\)/.test(files.profile)
    && /RegistrationFlowUtility\.SaveUserDataAndSyncCloud/.test(files.profile)
    && /SyncAuthUserProfile/.test(files.registration)
    && /NormalizeHttpUrl/.test(files.registration)
    && /UpdateUserProfile\(displayName, photoUrl/.test(files.registration)
    && /SaveUserData/.test(files.registration)
    && /ClearAccountAvatarCaches/.test(files.avatarUpload)
    && /GoogleUserInfoHelper\.ClearLocalAvatarCache/.test(files.avatarUpload)
    && /AppleUserInfoHelper\.ClearLocalAvatarCache/.test(files.avatarUpload)
    && /FacebookUserInfoHelper\.ClearLocalAvatarCache/.test(files.avatarUpload),
  memory:
    /MemoryUiStore\.LoadLatest/.test(files.memory)
    && /MemoryUiStore\.SaveCurrent/.test(files.memory)
    && /MemoryUiStore\.ClearAll/.test(files.memory)
    && /MemoryUiStore\.LoadLatest/.test(files.dialogUi)
    && /memoryCloudRefreshRequested/.test(files.dialogUi)
    && !/LoadMemorySource/.test(files.dialogUi)
    && /MemoryPrivacySettings\.ShareAllMemoryEnabled/.test(files.memory)
    && /GetMemorySourceForPrompt/.test(files.dialogSystem)
    && /MemoryUiStore\.SaveCurrent\(\)/.test(files.dialogSystem)
    && /GetMemorySourceForPrompt/.test(read("Assets/Scripts/Dialog/Data/DailyOracleService.cs"))
    && /GetPromptMemorySource/.test(read("Assets/Scripts/GameManager/MemoryPrivacySettings.cs"))
    && /MemoryCacheKey/.test(files.memoryStore)
    && /PendingCloudSaveKey/.test(files.memoryStore)
    && /PendingCloudDeleteKey/.test(files.memoryStore)
    && /LoadLatest[\s\S]*?HasPendingCloudDelete\(\)[\s\S]*?TrySyncPendingCloudDelete/.test(files.memoryStore)
    && /LoadLatest[\s\S]*?HasPendingCloudSave\(\)[\s\S]*?TrySyncPendingCloudSave/.test(files.memoryStore)
    && /SaveCurrent[\s\S]*?SaveLocalSource\(Source\)[\s\S]*?MarkCloudSavePending/.test(files.memoryStore)
    && /ClearAll[\s\S]*?SaveLocalSource\(Source\)[\s\S]*?MarkCloudDeletePending/.test(files.memoryStore)
    && /CLEAR_CONFIRM_SECONDS = 8f/.test(files.memory)
    && /个人偏好/.test(files.memory)
    && /关系记忆/.test(files.memory)
    && /占卜连续性/.test(files.memory)
    && /最近 Prompt/.test(files.memory)
    && /private const float DeleteConfirmSeconds = 6f/.test(files.memoryList)
    && /_pendingDeleteItemId != item\.Id \|\| Time\.time > _deleteConfirmDeadline/.test(files.memoryList)
    && /ToastManager\.ShowToast\("再次点击删除这条记忆"\)/.test(files.memoryList)
    && /ResetDeleteConfirm\(\)[\s\S]*?MemoryUiStore\.DeleteMemory\(item\.Id\)/.test(files.memoryList)
    && /SaveAndRefresh\("记忆已删除", "本地已删除，云端稍后同步"\)/.test(files.memoryList)
    && /已保存到本地，云端稍后同步/.test(files.memoryDetail)
    && /OnMemorySearchInputInputChange\(string text\)[\s\S]*?ResetDeleteConfirm\(\)/.test(files.memoryList),
  memoryPrivacy:
    /CreateRuntimeClearButton/.test(read("Assets/Scripts/UI/MemoryPrivacySettingsUI.cs"))
    && /CreateRuntimeClearConfirmModal/.test(read("Assets/Scripts/UI/MemoryPrivacySettingsUI.cs"))
    && /MemoryUiStore\.ClearAll/.test(read("Assets/Scripts/UI/MemoryPrivacySettingsUI.cs"))
    && /SetClearButtonsInteractable/.test(read("Assets/Scripts/UI/MemoryPrivacySettingsUI.cs"))
    && !/清空确认弹窗还未生成/.test(read("Assets/Scripts/UI/MemoryPrivacySettingsUI.cs")),
  account:
    /GetLoginTypeDisplayText/.test(files.account)
    && /GetFormattedRegTime/.test(files.account)
    && /ShowSelectWindow/.test(files.account)
    && /SignOut/.test(files.account)
    && /DeleteUser/.test(files.account)
    && /ClearData/.test(files.account),
  notification:
    /LoadNotificationSettings/.test(files.notion)
    && /SaveNotificationSettings/.test(files.notion)
    && /SetDailyOracle/.test(files.notion)
    && /SetDialogueReply/.test(files.notion)
    && /SetDivinationReturn/.test(files.notion)
    && /SetFriendInteraction/.test(files.notion)
    && /SetActivitySystem/.test(files.notion)
    && /ToggleReminderTime/.test(files.notion)
    && /HasPendingCloudSync/.test(files.notifManager)
    && /KEY_PENDING_CLOUD_SYNC/.test(files.notifManager)
    && /MarkCloudSyncPending/.test(files.notifManager)
    && /MarkCloudSyncComplete/.test(files.notifManager)
    && /settings\.HasPendingCloudSync[\s\S]*?SaveCloudSettings\(false\)/.test(files.notion)
    && /settings\.MarkCloudSyncPending\(\)/.test(files.notion)
    && /settings\.MarkCloudSyncComplete\(\)/.test(files.notion)
    && /RescheduleNotifications/.test(files.notifManager)
    && /NotifyDialogueReplyReady/.test(files.notifScheduler)
    && /NotificationUnreadState\.MarkUnread\(payload\)/.test(files.notifScheduler)
    && /NotificationUnreadState\.ClearUnread\(\)/.test(files.notion)
    && /NotificationUnreadBadge\.Attach\(uiComponent\.noticeButton\)/.test(files.my)
    && /NotificationUnreadBadge\.Attach\(uiComponent\.NotificationButton\)/.test(files.addFriend)
    && /NotificationUnreadBadge\.Attach\(uiComponent\.NotificationsButton\)/.test(files.contactsInvite)
    && /NotificationUnreadBadge\.Attach\(uiComponent\.NotificationsButton\)/.test(files.facebookInvite)
    && /NotificationUnreadBadge\.Attach\(uiComponent\.NotificationsButton\)/.test(files.userSearch)
    && /NotificationUnreadBadge\.Attach\(uiComponent\.NotificationButton\)/.test(files.createFriendSuccess)
    && /public static bool HasUnread/.test(files.notifUnread)
    && /class NotificationUnreadBadgeBinder/.test(files.notifUnread)
    && /NotifyDialogueReplyReady\(content\)/.test(files.dialogSystem)
    && /NotifyDialogueReplyReady\(fullContent\)/.test(files.dialogSystem),
  feedback:
    /SubmitFeedback\("community"/.test(files.feedback)
    && /SubmitFeedback\("chat"/.test(files.feedback)
    && /MergeLocalPendingFeedbackEntries/.test(files.feedback)
    && /SyncPendingFeedbackEntries/.test(files.feedback)
    && /SaveFeedbackEntriesToCache/.test(files.feedback)
    && /MirrorFeedbackForBackend/.test(files.firestore)
    && /public void LoadFeedback/.test(files.firestore)
    && /onComplete\?\.Invoke\(null\)/.test(files.firestore),
  followUs:
    /LoadPublicAppConfig/.test(files.follow)
    && /MergeWithDefaults/.test(files.follow)
    && /IsConfiguredSocialLink/.test(files.follow)
    && /Instagram/.test(files.follow)
    && /Facebook/.test(files.follow)
    && /TikTok/.test(files.follow)
    && /Pinterest/.test(files.follow)
    && /https:\/\/x\.com/.test(files.follow),
  unlockPro:
    /GetMembershipStatus/.test(files.unlock)
    && /LoadPublicAppConfig/.test(files.unlock)
    && /ConfigureProducts/.test(files.unlock)
    && /PurchaseSubscription/.test(files.unlock)
    && /RestorePurchases/.test(files.unlock)
    && /OpenSubscriptionManagement/.test(files.unlock)
    && /BuildProductButtonText/.test(files.unlock)
    && /SetPurchaseButtonsInteractable\(!isCurrentPro/.test(files.unlock)
    && /using Unity\.Services\.Core;/.test(files.iapBridge)
    && /InitializeStore\(\)[\s\S]*?EnsureUnityServicesInitialized\(\)[\s\S]*?UnityPurchasing\.Initialize/.test(files.iapBridge)
    && /UnityServices\.InitializeAsync/.test(files.iapBridge)
    && /UnityServices\.State == ServicesInitializationState\.Initialized/.test(files.iapBridge)
    && /pendingRestore\)[\s\S]*?BeginStoreRestoreTransactions\(\)/.test(files.iapBridge)
    && /GetExtension<IAppleExtensions>\(\)[\s\S]*?RestoreTransactions/.test(files.iapBridge)
    && /GetExtension<IGooglePlayStoreExtensions>\(\)[\s\S]*?RestoreTransactions/.test(files.iapBridge)
    && /OnRestoreReceiptSubmitted/.test(files.iapBridge),
};

const failed = Object.entries(checks)
  .filter(([, ok]) => !ok)
  .map(([name]) => name);

if (failed.length > 0) {
  console.error(`Missing My module requirement evidence: ${failed.join(", ")}`);
  process.exit(1);
}
NODE
then
  ok "My module requirement guard covers home, history/detail, profile, memory, account, notification, feedback, social links, and Pro"
else
  fail "My module requirement guard failed; see /tmp/moonly_readiness_my_module_requirements.log"
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

if grep_file ".build-stamp" "scripts/build-android-apk.sh" \
  && grep_file ".build-stamp" "scripts/check-release-blockers.sh"; then
  ok "Android APK build script writes a completion stamp and release gate uses it for freshness"
else
  fail "Android APK build freshness stamp is not wired through build and release gate scripts"
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

if grep_file "Assets/GameData" "scripts/check-release-blockers.sh" \
  && grep_file "Assets/Resources" "scripts/check-release-blockers.sh" \
  && grep_file "Assets/GamerFrameWork" "scripts/check-release-blockers.sh"; then
  ok "release blockers freshness check includes UI prefabs, Resources, and framework runtime assets"
else
  fail "release blockers freshness check is missing UI prefab, Resources, or framework runtime asset paths"
fi

if ! awk '/file_is_stale_against_sources\\(\\)/,/^}/ { print }' scripts/check-release-blockers.sh | grep -q "^[[:space:]]*functions[[:space:]]*\\\\"; then
  ok "mobile artifact freshness excludes Firebase Functions source; backend deployment is checked separately"
else
  fail "mobile artifact freshness should not be invalidated by Firebase Functions-only edits"
fi

if grep_file "window == null" "Assets/GamerFrameWork/UIFrameWork/Scripts/Runtime/Code/UIModule.cs" \
  && grep_file "LoadWindow2Res] path is null" "Assets/GamerFrameWork/UIFrameWork/Scripts/Runtime/Code/UIModule.cs" \
  && grep_file "LoadWindow2Res] load failed" "Assets/GamerFrameWork/UIFrameWork/Scripts/Runtime/Code/UIModule.cs"; then
  ok "UI module guards against missing WindowConfig entries and unloaded prefabs"
else
  fail "UI module missing null guards for failed window prefab loads"
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

if grep_file "Copy Init Release Env Command" "Assets/Editor/AppReadinessDiagnosticsMenu.cs"; then
  ok "Editor menu can copy release env init command"
else
  fail "Editor menu missing release env init command"
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

if bash scripts/check-firebase-sdk-versions.sh >/tmp/moonly_readiness_firebase_sdk_versions.log 2>&1; then
  ok "Firebase SDK version consistency check passed"
  if grep -q "^\[WARN\]" /tmp/moonly_readiness_firebase_sdk_versions.log; then
    warn "Firebase SDK version consistency check has non-blocking warnings; see /tmp/moonly_readiness_firebase_sdk_versions.log"
  fi
else
  fail "Firebase SDK version consistency check failed; see /tmp/moonly_readiness_firebase_sdk_versions.log"
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

if grep_file "ForegroundPresentationOption" "Assets/Scripts/GameManager/AppNotificationScheduler.cs" \
  && grep_file "RepeatInterval = TimeSpan.FromDays(1)" "Assets/Scripts/GameManager/AppNotificationScheduler.cs" \
  && grep_file "iOSNotificationCalendarTrigger" "Assets/Scripts/GameManager/AppNotificationScheduler.cs" \
  && grep_file "Repeats = true" "Assets/Scripts/GameManager/AppNotificationScheduler.cs"; then
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
  && grep_file "LoadNotificationSettings" "Assets/Scripts/UI/NotionUI.cs" \
  && grep_file "MarkCloudSyncPending" "Assets/Scripts/UI/NotionUI.cs" \
  && grep_file "HasPendingCloudSync" "Assets/Scripts/UI/NotionUI.cs" \
  && grep_file "KEY_PENDING_CLOUD_SYNC" "Assets/Scripts/GameManager/NotificationSettingsManager.cs"; then
  ok "notification settings UI syncs settings with Firestore and preserves offline local changes"
else
  fail "notification settings UI is not wired to Firestore settings sync or offline pending sync"
fi

if grep_file "SendPasswordResetEmail" "Assets/Scripts/Platform/FireBase/FirebaseAuthManager.cs" \
  && grep_file "SubmitPasswordReset" "Assets/Scripts/UI/LoginUI.cs"; then
  ok "email login UI supports Firebase password reset emails"
else
  fail "email login UI is missing Firebase password reset support"
fi

if grep_file "ttsSynthesize" "Assets/Scripts/TTS/TTSManager.cs" \
  && grep_file "Authorization.*Bearer" "Assets/Scripts/TTS/TTSManager.cs" \
  && grep_file "BuildTTSFunctionErrorMessage" "Assets/Scripts/TTS/TTSManager.cs" \
  && grep_file "ToUserFacingError" "Assets/Scripts/UI/DialogUI.cs" \
  && ! grep_file "配置 API 密钥" "Assets/Scripts/UI/DialogUI.cs"; then
  ok "dialogue TTS uses authenticated Cloud Function and maps service errors for users"
else
  fail "dialogue TTS is missing Cloud Function auth or user-facing error handling"
fi

if node <<'NODE' >/tmp/moonly_readiness_dialogue_input.log 2>&1
const fs = require("fs");
const dialog = fs.readFileSync("Assets/Scripts/UI/DialogUI.cs", "utf8");

const trimsUserInput = /string inputText = \(uiComponent\.questionInputField\.text \?\? string\.Empty\)\.Trim\(\)/.test(dialog)
  && /string\.IsNullOrWhiteSpace\(inputText\)/.test(dialog);
const preservesDraftOnMembershipBlock = /if \(!MembershipGate\.CanUse\(MembershipFeature\.DialogMessage\)\)[\s\S]*?RefreshSendButtonState\(inputText\);[\s\S]*?return;/.test(dialog)
  && /MembershipGate\.CanUse\(MembershipFeature\.DialogMessage\)[\s\S]*?if \(mIsLoading\)/.test(dialog);
const clearsOnlyAfterSend = /SendUserMessage\(inputText\);[\s\S]*?ClearQuestionInput\(\);/.test(dialog)
  && /private void ClearQuestionInput\(\)[\s\S]*?questionInputField\.text = string\.Empty/.test(dialog);
const refreshesSendButton = /OnquestionInputChange\(string text\)[\s\S]*?RefreshSendButtonState\(text\)/.test(dialog)
  && /OnquestionInputEnd\(string text\)[\s\S]*?RefreshSendButtonState\(text\)/.test(dialog)
  && /sendButton\.interactable = !string\.IsNullOrWhiteSpace\(value\)/.test(dialog);

if (!trimsUserInput || !preservesDraftOnMembershipBlock || !clearsOnlyAfterSend || !refreshesSendButton) {
  console.error(JSON.stringify({
    trimsUserInput,
    preservesDraftOnMembershipBlock,
    clearsOnlyAfterSend,
    refreshesSendButton,
  }, null, 2));
  process.exit(1);
}
NODE
then
  ok "dialogue input trims blank messages, preserves drafts on quota blocks, and updates send state"
else
  fail "dialogue input guard failed; see /tmp/moonly_readiness_dialogue_input.log"
fi

if node <<'NODE' >/tmp/moonly_readiness_today_oracle_chat.log 2>&1
const fs = require("fs");
const dialogUi = fs.readFileSync("Assets/Scripts/UI/DialogUI.cs", "utf8");
const dialogSystem = fs.readFileSync("Assets/Scripts/Dialog/Data/DialogSystem.cs", "utf8");
const dailyService = fs.readFileSync("Assets/Scripts/Dialog/Data/DailyOracleService.cs", "utf8");
const todayOracleUi = fs.readFileSync("Assets/Scripts/UI/TodayOracleUI.cs", "utf8");
const todayCardUi = fs.readFileSync("Assets/Scripts/UI/TodayCardUI.cs", "utf8");
const completeInterpretationUi = fs.readFileSync("Assets/Scripts/UI/CompleteInterpretationUI.cs", "utf8");

const uiSendsBuiltContent = /SendTodayOracleMessage\(\)[\s\S]*?BuildTodayCardMessageContent\(\)[\s\S]*?AddTodayDivinationMessage\(content\)/.test(dialogUi)
  && !/AddTodayDivinationMessage\(""\)/.test(dialogUi);
const systemBuildsPayloadSummary = /public string BuildTodayCardMessageContent\(\)[\s\S]*?todayCardPayload[\s\S]*?displayName[\s\S]*?orientation[\s\S]*?oracleText[\s\S]*?generatedAt/.test(dialogSystem);
const systemRejectsBlankContent = /public ChatMessageData AddTodayDivinationMessage\(string content\)[\s\S]*?string\.IsNullOrWhiteSpace\(content\) \? BuildTodayCardMessageContent\(\) : content\.Trim\(\)/.test(dialogSystem)
  && /mApiMessageHistory\.Add\(new DeepSeekAPI\.Message\("assistant", content\)\)/.test(dialogSystem);
const servicePayloadCarriesOracleText = /public TodayCardPayload GetTodayCardPayload\(\)[\s\S]*?IsCachedOracleFor\(CurrentCard, CurrentUpright\)[\s\S]*?oraclePayload = CachedPayload[\s\S]*?IsCachedPreparedReadingFor\(CurrentCard, CurrentUpright\)[\s\S]*?oraclePayload = CachedPreparedReading\?\.oraclePayload[\s\S]*?oracleText = oraclePayload\?\.oracle[\s\S]*?title = FirstNonEmpty\(oraclePayload\?\.title, "今日塔罗"\)/.test(dailyService);
const detailBuildsPayloadWithOracleText = /private TodayCardPayload BuildTodayCardPayloadForDialog\(\)[\s\S]*?payload\.oracleText = FirstNonEmpty/.test(todayCardUi)
  && /private void SyncTodayCardPayloadToDialogSystem\(\)[\s\S]*?SetTodayCardPayload\(payload\)/.test(todayCardUi);
const detailFollowupsCarryContext = /private void SendFollowupQuestion\(string question\)[\s\S]*?SyncTodayCardPayloadToDialogSystem\(\)[\s\S]*?EventSystem\.DispatchEvent\(GameDataStr\.CardTopicSelected, question\)/.test(todayCardUi);
const detailContinueAddsCard = /OnContinueChatButtonClick\(\)[\s\S]*?SyncTodayCardPayloadToDialogSystem\(\)[\s\S]*?SendTodayOracleMessage\(\)/.test(todayCardUi);
const legacyOracleUiCarriesRichPayload = /OnDeepChatButtonClick\(\)[\s\S]*?var payload = BuildTodayCardPayloadForDialog\(\)[\s\S]*?SetTodayCardPayload\(payload\)/.test(todayOracleUi)
  && /cardPayload = BuildTodayCardPayloadForDialog\(card, upright, oraclePayload\)/.test(todayOracleUi)
  && /cardPayload = BuildTodayCardPayloadForDialog\(card, upright, safePayload\)/.test(todayOracleUi)
  && /BuildTodayCardPayloadForDialog\(preparedReading\.card, preparedReading\.upright, preparedReading\.oraclePayload\)/.test(todayOracleUi)
  && /private TodayOraclePayload GetCachedOraclePayloadFor\(TarotCard card, bool upright\)[\s\S]*?IsCachedOracleFor\(card, upright\)[\s\S]*?IsCachedPreparedReadingFor\(card, upright\)/.test(todayOracleUi)
  && /private TodayCardPayload BuildTodayCardPayloadForDialog\(TarotCard card, bool upright, TodayOraclePayload oraclePayload\)[\s\S]*?payload\.oracleText = FirstNonEmpty\(payload\.oracleText, oraclePayload\?\.oracle\)/.test(todayOracleUi);
const completeInterpretationFollowupsCarryContext = /private void NavigateToDialogAndSend\(string message\)[\s\S]*?SyncTodayCardPayloadToDialogSystem\(\)[\s\S]*?EventSystem\.DispatchEvent\(GameDataStr\.CardTopicSelected, message\)/.test(completeInterpretationUi)
  && /private void SyncTodayCardPayloadToDialogSystem\(\)[\s\S]*?GetTodayCardPayload\(\)[\s\S]*?DialogSystem\.Instance\?\.SetTodayCardPayload\(payload\)/.test(completeInterpretationUi);

if (!uiSendsBuiltContent || !systemBuildsPayloadSummary || !systemRejectsBlankContent || !servicePayloadCarriesOracleText || !detailBuildsPayloadWithOracleText || !detailFollowupsCarryContext || !detailContinueAddsCard || !legacyOracleUiCarriesRichPayload || !completeInterpretationFollowupsCarryContext) {
  console.error(JSON.stringify({
    uiSendsBuiltContent,
    systemBuildsPayloadSummary,
    systemRejectsBlankContent,
    servicePayloadCarriesOracleText,
    detailBuildsPayloadWithOracleText,
    detailFollowupsCarryContext,
    detailContinueAddsCard,
    legacyOracleUiCarriesRichPayload,
    completeInterpretationFollowupsCarryContext,
  }, null, 2));
  process.exit(1);
}
NODE
then
  ok "today oracle chat entries carry card context and non-empty summaries into dialogue"
else
  fail "today oracle deep-chat guard failed; see /tmp/moonly_readiness_today_oracle_chat.log"
fi

if node <<'NODE' >/tmp/moonly_readiness_daily_oracle_history.log 2>&1
const fs = require("fs");
const dailyHistory = fs.readFileSync("Assets/Scripts/Dialog/Data/DailyOracleHistoryBridge.cs", "utf8");
const recordStore = fs.readFileSync("Assets/Scripts/Platform/FireBase/DivinationRecordFirestore.cs", "utf8");

const dailyHistorySavesLocalFirst = /private static DivinationRecordData SaveRecord\(DivinationRecordData record, bool saveCloud\)[\s\S]*?DivinationRecordFirestore\.SaveRecordLocal\(record\);[\s\S]*?DivinationRecordFirestore store = GetRecordStore\(\)/.test(dailyHistory);
const dailyHistoryTimeoutKeepsLocal = /历史服务暂未就绪，每日神谕历史已保存到本地缓存，云端稍后同步/.test(dailyHistory)
  && /历史服务不可用，已保存每日神谕历史到本地缓存/.test(dailyHistory)
  && !/未保存每日神谕历史/.test(dailyHistory);
const recordStoreSyncsAfterLogin = /authManager\.OnLoginSuccess \+= OnAuthLoginSuccess/.test(recordStore)
  && /authManager\.OnLogout \+= OnAuthLogout/.test(recordStore)
  && /private void OnAuthLoginSuccess\(AuthProvider provider, Firebase\.Auth\.FirebaseUser user\)[\s\S]*?SyncLocalCacheToCloud\(\)/.test(recordStore)
  && /private void OnAuthLogout\(\)[\s\S]*?_isSyncingLocalChanges = false/.test(recordStore);

if (!dailyHistorySavesLocalFirst || !dailyHistoryTimeoutKeepsLocal || !recordStoreSyncsAfterLogin) {
  console.error(JSON.stringify({
    dailyHistorySavesLocalFirst,
    dailyHistoryTimeoutKeepsLocal,
    recordStoreSyncsAfterLogin,
  }, null, 2));
  process.exit(1);
}
NODE
then
  ok "daily oracle history saves locally first and syncs local history cache after login"
else
  fail "daily oracle history fallback guard failed; see /tmp/moonly_readiness_daily_oracle_history.log"
fi

if node <<'NODE' >/tmp/moonly_readiness_daily_oracle_cloud_sync.log 2>&1
const fs = require("fs");
const dailyStore = fs.readFileSync("Assets/Scripts/Platform/FireBase/DailyOracleFirestore.cs", "utf8");
const dailyService = fs.readFileSync("Assets/Scripts/Dialog/Data/DailyOracleService.cs", "utf8");
const todayUi = fs.readFileSync("Assets/Scripts/UI/TodayOracleUI.cs", "utf8");

const hasPendingQueue = /PENDING_SAVE_KEY_PREFIX = "DailyOraclePendingSaves_"/.test(dailyStore)
  && /private class PendingDateList/.test(dailyStore)
  && /QueuePendingDailyOracleSave\(string date\)/.test(dailyStore)
  && /RemovePendingDailyOracleSave\(string date\)/.test(dailyStore)
  && /LoadPendingDailyOracleSaveDates\(\)/.test(dailyStore);
const saveMarksPendingBeforeCloud = /public void SaveByDate\(string date, TarotCard card,[\s\S]*?SaveByDateLocal\(date, card, upright, payload, locale\);[\s\S]*?QueuePendingDailyOracleSave\(date\);[\s\S]*?if \(!CheckReady\(onComplete\)\) return/.test(dailyStore);
const syncsPendingAfterReadyOrLogin = /OnFirebaseReady\(\)[\s\S]*?SyncPendingDailyOracleSaves\(\)/.test(dailyStore)
  && /OnAuthLoginSuccess\(AuthProvider provider, Firebase\.Auth\.FirebaseUser user\)[\s\S]*?SyncPendingDailyOracleSaves\(\)/.test(dailyStore)
  && /OnAuthLogout\(\)[\s\S]*?_isSyncingPendingSaves = false/.test(dailyStore);
const uploadClearsPendingAndSummary = /SaveRecordToCloud\(DailyOracleCloudRecord record, string uid,[\s\S]*?SetAsync\(BuildDailyOracleData\(record\), SetOptions\.MergeAll\)[\s\S]*?RemovePendingDailyOracleSave\(record\.date\)[\s\S]*?SaveSummaryFromRecord\(record, DailyDivinationSyncSettingsManager\.Instance\.GetSettings\(\)\)/.test(dailyStore);
const localFallbacksMarkPending = /SaveTodayLocalPending\(TarotCard card/.test(dailyStore)
  && /SaveByDateLocalPending\(string date/.test(dailyStore)
  && /QueuePendingDailyOracleSave\(string\.IsNullOrWhiteSpace\(date\) \? DateTime\.Now\.ToString\("yyyy-MM-dd"\) : date\)/.test(dailyStore)
  && /SaveTodayLocalPending/.test(dailyService)
  && /SaveTodayLocalPending/.test(todayUi);
const clearDeletesPending = /ClearLocalCacheForCurrentUser[\s\S]*?foreach \(string key in GetPendingSaveKeysForSync\(\)\)[\s\S]*?PlayerPrefs\.DeleteKey\(key\)/.test(dailyStore);

if (!hasPendingQueue || !saveMarksPendingBeforeCloud || !syncsPendingAfterReadyOrLogin || !uploadClearsPendingAndSummary || !localFallbacksMarkPending || !clearDeletesPending) {
  console.error(JSON.stringify({
    hasPendingQueue,
    saveMarksPendingBeforeCloud,
    syncsPendingAfterReadyOrLogin,
    uploadClearsPendingAndSummary,
    localFallbacksMarkPending,
    clearDeletesPending,
  }, null, 2));
  process.exit(1);
}
NODE
then
  ok "daily oracle cloud records save locally first and sync pending daily_oracles after login"
else
  fail "daily oracle cloud pending-save guard failed; see /tmp/moonly_readiness_daily_oracle_cloud_sync.log"
fi

if node <<'NODE' >/tmp/moonly_readiness_divination_autosave.log 2>&1
const fs = require("fs");
const engine = fs.readFileSync("Assets/Scripts/Dialog/Data/DivinationEngine.cs", "utf8");
const detail = fs.readFileSync("Assets/Scripts/UI/DivinationRecordUI.cs", "utf8");
const dialogSystem = fs.readFileSync("Assets/Scripts/Dialog/Data/DialogSystem.cs", "utf8");

const engineReportsLocalFallback = /SaveFromSession\(CurrentSession, shortVerdict,[\s\S]*?占卜记录已保存到本地缓存，云端稍后同步/.test(engine)
  && /DivinationRecordBuilder\.FromSession\(\)[\s\S]*?DivinationRecordFirestore\.SaveRecordLocal\(record\)/.test(engine);
const detailReportsLocalFallback = /bool localSaved = DivinationRecordFirestore\.SaveRecordLocal\(_currentRecord\)/.test(detail)
  && /localSaved \? "已保存到本地，云端稍后同步" : "保存失败，请稍后再试"/.test(detail)
  && /记录已保存到本地缓存/.test(detail);
const dialogReplySavesLocalFallback = /ApplyDivinationReplyToActiveSpread\(string reply\)[\s\S]*?SaveDivinationReplyRecord\(target\)/.test(dialogSystem)
  && /private void SaveDivinationReplyRecord\(ChatMessageData target\)[\s\S]*?DivinationRecordBuilder\.FromChatMessage\(target\)[\s\S]*?DivinationRecordFirestore\.SaveRecordLocal\(record\)[\s\S]*?DivinationRecordFirestore store = DivinationRecordFirestore\.Instance/.test(dialogSystem)
  && /占卜回复记录已保存到本地缓存，云端稍后同步/.test(dialogSystem);
const noStaleUnsavedMessaging = !/占卜记录未保存|跳过自动保存|历史服务未就绪，未保存记录/.test(engine + "\n" + detail + "\n" + dialogSystem);

if (!engineReportsLocalFallback || !detailReportsLocalFallback || !dialogReplySavesLocalFallback || !noStaleUnsavedMessaging) {
  console.error(JSON.stringify({
    engineReportsLocalFallback,
    detailReportsLocalFallback,
    dialogReplySavesLocalFallback,
    noStaleUnsavedMessaging,
  }, null, 2));
  process.exit(1);
}
NODE
then
  ok "divination auto-save, dialogue reply save, and detail save report local fallback instead of stale unsaved states"
else
  fail "divination auto-save fallback guard failed; see /tmp/moonly_readiness_divination_autosave.log"
fi

if grep_file "PhoneAuthProvider.GetInstance" "Assets/Scripts/UI/VerifyPhoneUI.cs" \
  && grep_file "VerifyPhoneNumber" "Assets/Scripts/UI/VerifyPhoneUI.cs" \
  && grep_file "UpdatePhoneNumberCredentialAsync" "Assets/Scripts/UI/VerifyPhoneUI.cs" \
  && grep_file "\"phoneVerified\"" "Assets/Scripts/UI/RegistrationFlowUtility.cs" \
  && ! grep_file "真实短信校验待接入" "Assets/Scripts/UI/VerifyPhoneUI.cs"; then
  ok "phone registration UI uses Firebase Phone Auth and records verified status"
else
  fail "phone registration UI still lacks Firebase Phone Auth verification"
fi

if grep_file "SignInWithGameCenter" "Assets/Scripts/Platform/FireBase/FirebaseAuthManager.cs" \
  && grep_file "GameCenterAuthProvider.GetCredentialAsync" "Assets/Scripts/Platform/FireBase/FirebaseAuthManager.cs" \
  && grep_file "OnGameCenterSignInButtonClick" "Assets/Scripts/UI/LoginUI.cs" \
  && grep_file "IsGameCenterAuthProviderResolved" "Assets/Scripts/GameManager/AppReadinessDiagnostics.cs"; then
  ok "Game Center login is wired to Firebase Auth provider"
else
  fail "Game Center login is not wired to Firebase Auth provider"
fi

if grep_file "Apple 登录仅支持 iOS 设备" "Assets/Scripts/Platform/FireBase/FirebaseAuthManager.cs" \
  && grep_file "Apple 账号关联仅支持 iOS 设备" "Assets/Scripts/Platform/FireBase/FirebaseAuthManager.cs" \
  && grep_file "FinishWithError(\"Apple 登录仅支持 iOS 设备\")" "Assets/Scripts/Platform/Apple/AppleSignInHelper.cs" \
  && grep_file "Editor 模拟模式" "Assets/Scripts/Platform/Apple/AppleSignInHelper.cs" \
  && grep_file "ApplyPlatformButtonVisibility" "Assets/Scripts/UI/LoginUI.cs" \
  && grep_file "IsAppleSignInVisible" "Assets/Scripts/UI/LoginUI.cs" \
  && grep_file "IsGoogleSignInVisible" "Assets/Scripts/UI/LoginUI.cs" \
  && grep_file "IsFacebookSignInVisible" "Assets/Scripts/UI/LoginUI.cs" \
  && grep_file "IsGameCenterSignInVisible" "Assets/Scripts/UI/LoginUI.cs" \
  && grep_file "Facebook.Unity.FB, Facebook.Unity" "Assets/Scripts/Platform/Facebook/FacebookSignInHelper.cs" \
  && grep_file "Facebook.Unity.FacebookDelegate\`1, Facebook.Unity" "Assets/Scripts/Platform/Facebook/FacebookSignInHelper.cs" \
  && grep_file "typeof(IEnumerable<string>)" "Assets/Scripts/Platform/Facebook/FacebookSignInHelper.cs" \
  && grep_file "ResolveConfiguredFacebookAppId" "Assets/Scripts/Platform/Facebook/FacebookSignInHelper.cs" \
  && grep_file "RequestFriendDiscoveryAccess" "Assets/Scripts/Platform/Facebook/FacebookSignInHelper.cs" \
  && grep_file "FriendDiscoveryPermissions" "Assets/Scripts/Platform/Facebook/FacebookSignInHelper.cs" \
  && grep_file "RequestFriendDiscoveryAccess(anchor" "Assets/Scripts/Platform/Facebook/FacebookFriendDiscoveryManager.cs" \
  && grep_file "IsPermissionError" "Assets/Scripts/Platform/Facebook/FacebookFriendDiscoveryManager.cs"; then
  ok "platform-specific sign-in buttons, Facebook SDK wiring, and friend-discovery permission flow are guarded"
else
  fail "platform-specific sign-in guards, Facebook SDK reflection wiring, or friend-discovery permission flow are missing"
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
  && grep_file "ScheduleTomorrowHookReminder" "Assets/Scripts/UI/DialogUI.cs" \
  && grep_file "NotifyDialogueReplyReady" "Assets/Scripts/GameManager/AppNotificationScheduler.cs" \
  && grep_file "dialogue={settings.DialogueReplyEnabled}" "Assets/Scripts/GameManager/AppReadinessDiagnostics.cs" \
  && grep_file "NotifyDialogueReplyReady(content)" "Assets/Scripts/Dialog/Data/DialogSystem.cs" \
  && grep_file "NotifyDialogueReplyReady(fullContent)" "Assets/Scripts/Dialog/Data/DialogSystem.cs"; then
  ok "dialogue reply, friend, relationship, daily-oracle feed, and Tomorrow Hook notification triggers are wired"
else
  fail "one or more notification trigger call sites are missing"
fi

if node <<'NODE' >/tmp/moonly_readiness_dialogue_remote_push.log 2>&1
const fs = require("fs");

const functionsIndex = fs.readFileSync("functions/index.js", "utf8");
const deepSeekApi = fs.readFileSync("Assets/Scripts/Dialog/DeepSeekAPI.cs", "utf8");
const dialogSystem = fs.readFileSync("Assets/Scripts/Dialog/Data/DialogSystem.cs", "utf8");
const dialogHistory = fs.readFileSync("Assets/Scripts/Platform/FireBase/DialogHistoryFirestore.cs", "utf8");
const remotePush = fs.readFileSync("Assets/Scripts/GameManager/RemotePushManager.cs", "utf8");
const rules = fs.readFileSync("firestore.rules", "utf8");
const deploy = fs.readFileSync("scripts/deploy-firebase.sh", "utf8");

const functionsQueuesReply = /function shouldNotifyDialogueReply/.test(functionsIndex)
  && /async function enqueueDialogueReplyNotification/.test(functionsIndex)
  && /async function completeDialogueReplyJob/.test(functionsIndex)
  && /exports\.processStaleDialogueReplyJobs = onSchedule/.test(functionsIndex)
  && /collectionGroup\(DIALOG_REPLY_JOBS_COLLECTION\)/.test(functionsIndex)
  && /collection\(PUSH_OUTBOX_COLLECTION\)/.test(functionsIndex)
  && /preferenceKey: "dialogueReplyEnabled"/.test(functionsIndex)
  && /remote_notifications/.test(functionsIndex);
const clientCanRequestReplyPush = /AppendNotificationOptions/.test(deepSeekApi)
  && /notifyOnComplete/.test(deepSeekApi)
  && /notificationType\\":\\"dialogue_reply/.test(deepSeekApi)
  && /clientRequestId/.test(deepSeekApi);
const dialogOptsIn = /QueueDialogueReplyJob\(replyJobId, messages/.test(dialogSystem)
  && /SendChatRequest\(messages,[\s\S]*?true,[\s\S]*?replyJobId/.test(dialogSystem)
  && /SendChatRequestStream\(messages,[\s\S]*?true,[\s\S]*?replyJobId/.test(dialogSystem);
const clientQueuesFallbackJob = /REPLY_JOBS_COLLECTION_NAME = "dialog_reply_jobs"/.test(dialogHistory)
  && /QueueDialogueReplyJob/.test(dialogHistory)
  && /"status", "client_streaming"/.test(dialogHistory);
const foregroundDedupesReply = /IsDialogueReplyPush/.test(remotePush)
  && /Application\.isFocused/.test(remotePush)
  && /dialogue_reply/.test(remotePush);
const rulesAllowClientCreate = /match \/dialog_reply_jobs\/\{jobId\}[\s\S]*?allow read, create, delete: if isOwner\(uid\);[\s\S]*?allow update: if false;/.test(rules);
const deploysScheduler = /functions:processStaleDialogueReplyJobs/.test(deploy);

if (!functionsQueuesReply) throw new Error("functions dialogue reply outbox wiring missing");
if (!clientCanRequestReplyPush) throw new Error("DeepSeekAPI notify-on-complete payload missing");
if (!dialogOptsIn) throw new Error("DialogSystem does not opt chat requests into remote reply push");
if (!clientQueuesFallbackJob) throw new Error("DialogHistoryFirestore does not queue fallback reply jobs");
if (!foregroundDedupesReply) throw new Error("RemotePushManager foreground dialogue dedupe missing");
if (!rulesAllowClientCreate) throw new Error("Firestore rules do not allow owned dialogue reply job creation safely");
if (!deploysScheduler) throw new Error("deploy script does not include scheduled dialogue reply worker");
NODE
then
  ok "dialogue reply remote push and offline fallback job wiring are present"
else
  fail "dialogue reply remote push/offline fallback wiring is incomplete; see /tmp/moonly_readiness_dialogue_remote_push.log"
fi

if node <<'NODE' >/tmp/moonly_readiness_virtual_friend_sync.log 2>&1
const fs = require("fs");
const firestore = fs.readFileSync("Assets/Scripts/Platform/FireBase/FirestoreManager.cs", "utf8");
const create = fs.readFileSync("Assets/Scripts/UI/CreateFriendUI.cs", "utf8");
const edit = fs.readFileSync("Assets/Scripts/UI/EditFriendUI.cs", "utf8");
const friendUi = fs.readFileSync("Assets/Scripts/UI/FriendUI.cs", "utf8");
const friendMove = fs.readFileSync("Assets/Scripts/UI/FriendMoveUI.cs", "utf8");

const hasPendingQueues = /PENDING_VIRTUAL_FRIEND_SAVE_KEY_PREFIX/.test(firestore)
  && /PENDING_VIRTUAL_FRIEND_DELETE_KEY_PREFIX/.test(firestore)
  && /QueueVirtualFriendSaveLocal/.test(firestore)
  && /QueueVirtualFriendDeleteLocal/.test(firestore)
  && /SyncPendingVirtualFriendSaves/.test(firestore)
  && /SyncPendingVirtualFriendDeletes/.test(firestore);
const syncsOnReady = /OnFirebaseReady\(\)[\s\S]*?SyncPendingVirtualFriendSaves\(\)[\s\S]*?SyncPendingVirtualFriendDeletes\(\)/.test(firestore);
const saveQueuesAndClears = /public void SaveVirtualFriend\(FriendDataManager\.FriendData virtualFriend[\s\S]*?QueuePendingVirtualFriendSave\(virtualFriend\.virtualFriendId\)[\s\S]*?CommitSaveVirtualFriendWithAvatar/.test(firestore)
  && /RemovePendingVirtualFriendSave\(virtualFriend\.virtualFriendId\)/.test(firestore);
const loadPreservesLocalPending = /public void LoadVirtualFriends[\s\S]*?SyncPendingVirtualFriendSaves\(\)[\s\S]*?SyncPendingVirtualFriendDeletes\(\)[\s\S]*?IsVirtualFriendPendingSave\(doc\.Id\) \|\| IsVirtualFriendPendingDelete\(doc\.Id\)/.test(firestore);
const deleteQueuesAndRemovesLocal = /public void DeleteVirtualFriend\(FriendDataManager\.FriendData virtualFriend[\s\S]*?QueueVirtualFriendDeleteLocal\(virtualFriendId\)[\s\S]*?RemoveVirtualFriendById\(virtualFriendId\)/.test(firestore)
  && /CommitDeleteVirtualFriend/.test(firestore)
  && /RemovePendingVirtualFriendDelete\(virtualFriendId\)/.test(firestore);
const createQueuesWhenStoreMissing = /QueueVirtualFriendSaveLocal\(createdFriend\)/.test(create)
  && /QueueVirtualFriendSaveLocal\(friend\)/.test(create);
const editQueuesWhenStoreMissing = /QueueVirtualFriendSaveLocal\(currentFriend\)/.test(edit)
  && /SaveVirtualFriend\(currentFriend, _ => \{ \}\)/.test(edit);
const deleteEntrypointsQueue = /QueueVirtualFriendDeleteLocal\(friend\)/.test(friendUi)
  && /已删除创建的好友，云端稍后同步/.test(friendUi)
  && /QueueVirtualFriendDeleteLocal\(friend\)/.test(friendMove)
  && /已删除创建的好友，云端稍后同步/.test(friendMove);

if (!hasPendingQueues || !syncsOnReady || !saveQueuesAndClears || !loadPreservesLocalPending || !deleteQueuesAndRemovesLocal || !createQueuesWhenStoreMissing || !editQueuesWhenStoreMissing || !deleteEntrypointsQueue) {
  console.error(JSON.stringify({
    hasPendingQueues,
    syncsOnReady,
    saveQueuesAndClears,
    loadPreservesLocalPending,
    deleteQueuesAndRemovesLocal,
    createQueuesWhenStoreMissing,
    editQueuesWhenStoreMissing,
    deleteEntrypointsQueue,
  }, null, 2));
  process.exit(1);
}
NODE
then
  ok "created-friend cloud save/delete paths preserve local changes and sync pending virtual friends later"
else
  fail "created-friend cloud sync queue is incomplete; see /tmp/moonly_readiness_virtual_friend_sync.log"
fi

if node <<'NODE' >/tmp/moonly_readiness_real_friend_sync.log 2>&1
const fs = require("fs");
const firestore = fs.readFileSync("Assets/Scripts/Platform/FireBase/FirestoreManager.cs", "utf8");
const friendUi = fs.readFileSync("Assets/Scripts/UI/FriendUI.cs", "utf8");
const friendMove = fs.readFileSync("Assets/Scripts/UI/FriendMoveUI.cs", "utf8");

const queuesDeletesOnCommitFailure = /CommitRemoveRealFriend\(currentUid, friendUid, success =>[\s\S]*?if \(!success\)[\s\S]*?QueuePendingRealFriendDelete\(friendUid\)[\s\S]*?RemoveRealFriendByFirebaseUid\(friendUid\)[\s\S]*?onComplete\?\.Invoke\(true\)/.test(firestore);
const queuesBlocksOnCommitFailure = /CommitBlockRealFriend\(currentUid, friendUid, friend, success =>[\s\S]*?if \(!success\)[\s\S]*?QueuePendingRealFriendBlock\(friendUid\)[\s\S]*?AddBlockedUser\(friendUid\)[\s\S]*?onComplete\?\.Invoke\(true\)/.test(firestore);
const exposesQueueState = /public static bool IsRealFriendDeleteQueuedLocal\(string friendUid\)/.test(firestore)
  && /public static bool IsRealFriendBlockQueuedLocal\(string friendUid\)/.test(firestore)
  && /public static bool IsVirtualFriendDeleteQueuedLocal\(string virtualFriendId\)/.test(firestore);
const friendUiShowsQueuedDelete = /IsVirtualFriendDeleteQueuedLocal\(friend\.virtualFriendId\)/.test(friendUi)
  && /IsRealFriendDeleteQueuedLocal\(friend\.firebaseUid\)/.test(friendUi)
  && /BuildRealFriendDeleteSuccessToast\(bool queued\)/.test(friendUi)
  && /已删除好友，云端稍后同步/.test(friendUi);
const friendMoveShowsQueuedDeleteAndBlock = /IsVirtualFriendDeleteQueuedLocal\(friend\.virtualFriendId\)/.test(friendMove)
  && /IsRealFriendDeleteQueuedLocal\(friend\.firebaseUid\)/.test(friendMove)
  && /IsRealFriendBlockQueuedLocal\(friend\.firebaseUid\)/.test(friendMove)
  && /BuildRealFriendDeleteSuccessToast\(bool queued\)/.test(friendMove)
  && /已屏蔽好友，云端稍后同步/.test(friendMove);

if (!queuesDeletesOnCommitFailure || !queuesBlocksOnCommitFailure || !exposesQueueState || !friendUiShowsQueuedDelete || !friendMoveShowsQueuedDeleteAndBlock) {
  console.error(JSON.stringify({
    queuesDeletesOnCommitFailure,
    queuesBlocksOnCommitFailure,
    exposesQueueState,
    friendUiShowsQueuedDelete,
    friendMoveShowsQueuedDeleteAndBlock,
  }, null, 2));
  process.exit(1);
}
NODE
then
  ok "real-friend delete/block commit failures preserve local changes and sync pending actions later"
else
  fail "real-friend pending delete/block fallback is incomplete; see /tmp/moonly_readiness_real_friend_sync.log"
fi

if node <<'NODE' >/tmp/moonly_readiness_virtual_relationship.log 2>&1
const fs = require("fs");
const helper = fs.readFileSync("Assets/Scripts/Friend/CreatedFriendRelationshipDivinationLocalFlow.cs", "utf8");
const createInfo = fs.readFileSync("Assets/Scripts/UI/CreateFriendInfoUI.cs", "utf8");
const friendMove = fs.readFileSync("Assets/Scripts/UI/FriendMoveUI.cs", "utf8");
const friendRuntime = fs.readFileSync("Assets/Scripts/Friend/FriendRuntimeUI.cs", "utf8");
const inviteConfirm = fs.readFileSync("Assets/Scripts/UI/TwoPersonDivinationInviteConfirmFlowUI.cs", "utf8");
const flow = fs.readFileSync("Assets/Scripts/Friend/RelationshipDivinationFlow.cs", "utf8");
const service = fs.readFileSync("Assets/Scripts/Platform/FireBase/RelationshipDivinationFirestore.cs", "utf8");

const helperCreatesLocalRecord = /public static bool TryStart\(FriendDataManager\.FriendData friend\)[\s\S]*?RelationshipDivinationFlow\.ShowRecord\(record, friend\)/.test(helper)
  && /status = RelationshipDivinationStatus\.Completed/.test(helper)
  && /isLocalOnly = true/.test(helper)
  && /TarotDeck\.DrawMultiple\(3\)/.test(helper);
const createInfoEntry = /RelationshipDivinationButtonName = "RelationshipDivinationButton"/.test(createInfo)
  && /RefreshRelationshipDivinationButton\(\)/.test(createInfo)
  && /CreatedFriendRelationshipDivinationLocalFlow\.TryStart\(currentFriend\)/.test(createInfo);
const friendMoveLocalEntry = /OnSendOracleRelatonButtonClick[\s\S]*?CreatedFriendRelationshipDivinationLocalFlow\.CanHandle\(currentFriend\)[\s\S]*?CreatedFriendRelationshipDivinationLocalFlow\.TryStart\(capturedLocal\)/.test(friendMove)
  && /canUseRelationshipDivination[\s\S]*?CreatedFriendRelationshipDivinationLocalFlow\.CanHandle\(currentFriend\)/.test(friendMove);
const overlayLocalEntry = /StartForFriend[\s\S]*?CreatedFriendRelationshipDivinationLocalFlow\.TryStart\(friend\)/.test(friendRuntime);
const confirmLocalEntry = /private void CreateInvite\(\)[\s\S]*?CreatedFriendRelationshipDivinationLocalFlow\.TryStart\(currentFriend\)/.test(inviteConfirm);
const genericFlowAllowsLocal = /ShowInviteConfirm[\s\S]*?CreatedFriendRelationshipDivinationLocalFlow\.TryStart\(friend\)/.test(flow)
  && /TryOpenActiveOrCreate[\s\S]*?CreatedFriendRelationshipDivinationLocalFlow\.CanHandle\(friend\)[\s\S]*?onCanCreate\?\.Invoke\(\)/.test(flow)
  && /CanUseTwoPersonDivination[\s\S]*?CreatedFriendRelationshipDivinationLocalFlow\.CanHandle\(friend\)\) return true/.test(flow)
  && /自建好友 · 本地关系占卜/.test(flow);
const serviceLocalFallback = /if \(friend\.isVirtual\)[\s\S]*?CreatedFriendRelationshipDivinationLocalFlow\.CreateRecord\(friend, question\)[\s\S]*?onComplete\?\.Invoke\(localRecord\)/.test(service)
  && !/自己创建的好友暂不支持双人占卜/.test(service);

if (!helperCreatesLocalRecord || !createInfoEntry || !friendMoveLocalEntry || !overlayLocalEntry || !confirmLocalEntry || !genericFlowAllowsLocal || !serviceLocalFallback) {
  console.error(JSON.stringify({ helperCreatesLocalRecord, createInfoEntry, friendMoveLocalEntry, overlayLocalEntry, confirmLocalEntry, genericFlowAllowsLocal, serviceLocalFallback }, null, 2));
  process.exit(1);
}
NODE
then
  ok "created-friend relationship divination is wired as a local completed reading across UI and service paths"
else
  fail "created-friend local relationship divination flow is missing or disabled; see /tmp/moonly_readiness_virtual_relationship.log"
fi

if grep_file "ApplySyncSettingsToPublishedSummaries" "Assets/Scripts/Platform/FireBase/DailyOracleFirestore.cs" \
  && grep_file "UpdateExistingSummarySettingsByDate" "Assets/Scripts/Platform/FireBase/DailyOracleFirestore.cs" \
  && grep_file "PublishSummaryByDate" "Assets/Scripts/Platform/FireBase/DailyOracleFirestore.cs" \
  && grep_file "HasPendingCloudSync" "Assets/Scripts/GameManager/DailyDivinationSyncSettingsManager.cs" \
  && grep_file "KEY_PENDING_CLOUD_SYNC" "Assets/Scripts/GameManager/DailyDivinationSyncSettingsManager.cs" \
  && grep_file "MarkCloudSyncPending" "Assets/Scripts/GameManager/DailyDivinationSyncSettingsManager.cs" \
  && grep_file "MarkCloudSyncComplete" "Assets/Scripts/GameManager/DailyDivinationSyncSettingsManager.cs" \
  && grep_file "HasPendingCloudSync" "Assets/Scripts/UI/DailyDivinationSyncSettingsUI.cs" \
  && grep_file "SaveSettings(false)" "Assets/Scripts/UI/DailyDivinationSyncSettingsUI.cs" \
  && grep_file "MarkCloudSyncPending" "Assets/Scripts/UI/DailyDivinationSyncSettingsUI.cs" \
  && grep_file "MarkCloudSyncComplete" "Assets/Scripts/UI/DailyDivinationSyncSettingsUI.cs" \
  && grep_file "UpdatePublishedSummaries(settings, showToast)" "Assets/Scripts/UI/DailyDivinationSyncSettingsUI.cs" \
  && grep_file "SaveCurrentSyncSettings(false)" "Assets/Scripts/UI/FriendProfileUI.cs" \
  && grep_file "syncSettingsVersion" "Assets/Scripts/UI/FriendProfileUI.cs" \
  && grep_file "MarkCloudSyncPending" "Assets/Scripts/UI/FriendProfileUI.cs" \
  && grep_file "MarkCloudSyncComplete" "Assets/Scripts/UI/FriendProfileUI.cs" \
  && grep_file "SaveCurrentSyncSettings(false)" "Assets/Scripts/UI/CreateFriendInfoUI.cs" \
  && grep_file "syncSettingsVersion" "Assets/Scripts/UI/CreateFriendInfoUI.cs" \
  && grep_file "MarkCloudSyncPending" "Assets/Scripts/UI/CreateFriendInfoUI.cs" \
  && grep_file "MarkCloudSyncComplete" "Assets/Scripts/UI/CreateFriendInfoUI.cs" \
  && grep_file "SaveCurrentSyncSettings(false)" "Assets/Scripts/UI/EditFriendUI.cs" \
  && grep_file "syncSettingsVersion" "Assets/Scripts/UI/EditFriendUI.cs" \
  && grep_file "MarkCloudSyncPending" "Assets/Scripts/UI/EditFriendUI.cs" \
  && grep_file "MarkCloudSyncComplete" "Assets/Scripts/UI/EditFriendUI.cs" \
  && grep_file "ApplySyncSettingsToPublishedSummaries(settings, 30" "Assets/Scripts/UI/DailyDivinationSyncSettingsUI.cs" \
  && grep_file "ApplySyncSettingsToPublishedSummaries(settings, 30" "Assets/Scripts/UI/FriendProfileUI.cs" \
  && grep_file "ApplySyncSettingsToPublishedSummaries(settings, 30" "Assets/Scripts/UI/CreateFriendInfoUI.cs" \
  && grep_file "ApplySyncSettingsToPublishedSummaries(settings, 30" "Assets/Scripts/UI/EditFriendUI.cs" \
  && grep_file "canReadDailyOracleSummary" "firestore.rules" \
  && grep_file "isAcceptedFriend(data.ownerUid)" "firestore.rules"; then
  ok "daily oracle sync settings refresh existing summaries, preserve offline local changes, and keep Firestore friend-only read rules"
else
  fail "daily oracle sync settings are missing offline sync, existing-summary refresh wiring, or friend-only read rules"
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
      for function_name in membershipStatus readinessStatus publicConfig submitFeedback adminPublicConfigUpdate adminFeedbackList adminFeedbackUpdate deleteMyAccountData sendTestPush friendRequestRemotePush relationshipInviteRemotePush remoteNotificationOutboxPush processStaleDialogueReplyJobs; do
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
  if dotnet build --no-restore Assembly-CSharp.csproj -p:UseSharedCompilation=false \
    && dotnet build --no-restore Assembly-CSharp-Editor.csproj -p:UseSharedCompilation=false; then
    ok "C# build checks passed"
  else
    fail "C# build checks failed; see command output above"
  fi

  iap_defines="$(sed -n 's:.*<DefineConstants>\(.*\)</DefineConstants>.*:\1:p' Assembly-CSharp.csproj | head -n 1)"
  if [[ -n "$iap_defines" ]]; then
    case ";$iap_defines;" in
      *";UNITY_PURCHASING;"*)
        ok "Unity IAP bridge compile define is present and covered by C# build checks"
        ;;
      *)
        fail "Assembly-CSharp.csproj missing UNITY_PURCHASING define for IAP bridge compile coverage"
        ;;
    esac
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
