#!/usr/bin/env bash
set -euo pipefail

PROJECT_ID="${FIREBASE_PROJECT:-fari-app-b2fd2}"
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
READINESS_URL="${READINESS_URL:-https://us-central1-$PROJECT_ID.cloudfunctions.net/readinessStatus}"
DEPLOY_INCOMPLETE_FUNCTIONS="${MOONLY_DEPLOY_INCOMPLETE_FUNCTIONS:-0}"
SECRET_BINDINGS_FILE="$ROOT_DIR/functions/.moonly-secret-bindings.json"
RESOLVED_SECRET_NAMES=()
DEPLOY_TARGETS=()
SKIPPED_TARGETS=()

cleanup_secret_bindings() {
  rm -f "$SECRET_BINDINGS_FILE"
}
trap cleanup_secret_bindings EXIT

join_by_comma() {
  local IFS=","
  echo "$*"
}

secret_enabled() {
  local name="$1"
  [[ "${MOONLY_BIND_ALL_SECRETS:-0}" == "1" ]] && return 0
  [[ " ${RESOLVED_SECRET_NAMES[*]:-} " == *" $name "* ]]
}

resolve_secret_bindings() {
  if [[ "${MOONLY_BIND_ALL_SECRETS:-0}" == "1" ]]; then
    RESOLVED_SECRET_NAMES=(
      DEEPSEEK_API_KEY
      VOLC_TTS_API_KEY
      PAYMENT_WEBHOOK_SECRET
      APPLE_SHARED_SECRET
      GOOGLE_PACKAGE_NAME
      GOOGLE_SERVICE_ACCOUNT_JSON
    )
    export MOONLY_DEPLOYED_SECRETS="all"
    return
  fi

  RESOLVED_SECRET_NAMES=()
  if [[ -n "${MOONLY_DEPLOYED_SECRETS:-}" ]]; then
    # shellcheck disable=SC2206
    local requested=(${MOONLY_DEPLOYED_SECRETS//,/ })
    local name
    for name in "${requested[@]}"; do
      [[ -n "$name" ]] && RESOLVED_SECRET_NAMES+=("$name")
    done
    export MOONLY_DEPLOYED_SECRETS="$(join_by_comma "${RESOLVED_SECRET_NAMES[@]}")"
    return
  fi

  export MOONLY_DEPLOYED_SECRETS=""
}

build_default_targets() {
  DEPLOY_TARGETS=(
    "firestore:rules"
    "firestore:indexes"
    "storage"
    "functions:membershipStatus"
    "functions:readinessStatus"
    "functions:publicConfig"
    "functions:submitFeedback"
    "functions:adminPublicConfigUpdate"
    "functions:adminDashboardSummary"
    "functions:adminConfigOverview"
    "functions:adminUserSearch"
    "functions:adminUserDetail"
    "functions:adminMembershipOverview"
    "functions:adminCreateRegisteredUser"
    "functions:adminGrantPro"
    "functions:adminRevokePro"
    "functions:adminFeedbackList"
    "functions:adminFeedbackUpdate"
    "functions:deleteMyAccountData"
    "functions:sendTestPush"
    "functions:friendRequestRemotePush"
    "functions:relationshipInviteRemotePush"
    "functions:remoteNotificationOutboxPush"
  )
  SKIPPED_TARGETS=()

  if [[ "${MOONLY_DEPLOY_HOSTING:-0}" == "1" ]]; then
    DEPLOY_TARGETS+=("hosting")
  fi

  if secret_enabled DEEPSEEK_API_KEY || [[ "$DEPLOY_INCOMPLETE_FUNCTIONS" == "1" ]]; then
    DEPLOY_TARGETS+=("functions:aiChat" "functions:aiChatStream" "functions:processStaleDialogueReplyJobs")
  else
    SKIPPED_TARGETS+=("aiChat/aiChatStream/processStaleDialogueReplyJobs need DEEPSEEK_API_KEY")
  fi

  if secret_enabled VOLC_TTS_API_KEY || [[ "$DEPLOY_INCOMPLETE_FUNCTIONS" == "1" ]]; then
    DEPLOY_TARGETS+=("functions:ttsSynthesize")
  else
    SKIPPED_TARGETS+=("ttsSynthesize needs VOLC_TTS_API_KEY")
  fi

  if {
    secret_enabled APPLE_SHARED_SECRET \
      && secret_enabled GOOGLE_PACKAGE_NAME \
      && secret_enabled GOOGLE_SERVICE_ACCOUNT_JSON
  } || [[ "$DEPLOY_INCOMPLETE_FUNCTIONS" == "1" ]]; then
    DEPLOY_TARGETS+=("functions:submitIapReceipt")
  else
    SKIPPED_TARGETS+=("submitIapReceipt needs APPLE_SHARED_SECRET + GOOGLE_PACKAGE_NAME + GOOGLE_SERVICE_ACCOUNT_JSON")
  fi

  if secret_enabled PAYMENT_WEBHOOK_SECRET || [[ "$DEPLOY_INCOMPLETE_FUNCTIONS" == "1" ]]; then
    DEPLOY_TARGETS+=("functions:paymentWebhook")
  else
    SKIPPED_TARGETS+=("paymentWebhook needs PAYMENT_WEBHOOK_SECRET")
  fi
}

write_secret_bindings_file() {
  node - "$SECRET_BINDINGS_FILE" "${MOONLY_DEPLOYED_SECRETS:-}" <<'NODE'
const fs = require("fs");
const path = process.argv[2];
const raw = String(process.argv[3] || "").trim();
const allSecrets = [
  "DEEPSEEK_API_KEY",
  "VOLC_TTS_API_KEY",
  "PAYMENT_WEBHOOK_SECRET",
  "APPLE_SHARED_SECRET",
  "GOOGLE_PACKAGE_NAME",
  "GOOGLE_SERVICE_ACCOUNT_JSON",
];
const secrets = raw === "all"
  ? allSecrets
  : raw.split(",").map((name) => name.trim()).filter(Boolean);
fs.writeFileSync(path, `${JSON.stringify({ secrets }, null, 2)}\n`);
NODE
}

if [[ -n "${MOONLY_PROXY:-}" ]]; then
  export HTTPS_PROXY="$MOONLY_PROXY"
  export HTTP_PROXY="$MOONLY_PROXY"
fi

if [[ -n "${MOONLY_ALL_PROXY:-}" ]]; then
  export ALL_PROXY="$MOONLY_ALL_PROXY"
fi

cd "$ROOT_DIR"
resolve_secret_bindings
build_default_targets
write_secret_bindings_file
DEPLOY_ONLY="${FIREBASE_ONLY:-$(join_by_comma "${DEPLOY_TARGETS[@]}")}"

if ! command -v firebase >/dev/null 2>&1; then
  echo "firebase CLI not found. Install firebase-tools first." >&2
  exit 127
fi

if ! command -v node >/dev/null 2>&1; then
  echo "node not found. Node is required for post-deploy readiness checks." >&2
  exit 127
fi

if ! command -v curl >/dev/null 2>&1; then
  echo "curl not found. curl is required for post-deploy readiness checks." >&2
  exit 127
fi

if ! firebase login:list --json >/tmp/moonly_firebase_login.json 2>/tmp/moonly_firebase_login.err; then
  echo "Firebase CLI is not logged in. Run: firebase login --reauth" >&2
  exit 2
fi

if ! node -e 'const fs=require("fs"); const data=JSON.parse(fs.readFileSync("/tmp/moonly_firebase_login.json","utf8")); process.exit(Array.isArray(data.result)&&data.result.length>0?0:1);' >/dev/null 2>&1; then
  echo "Firebase CLI has no active login. Run: firebase login --reauth" >&2
  exit 2
fi

if ! firebase use --json >/tmp/moonly_firebase_use.json 2>/tmp/moonly_firebase_use.err; then
  echo "Firebase project is not selected. Run: firebase use $PROJECT_ID" >&2
  exit 2
fi

if ! node - "$PROJECT_ID" <<'NODE'
const fs = require("fs");
const expectedProject = process.argv[2];
const data = JSON.parse(fs.readFileSync("/tmp/moonly_firebase_use.json", "utf8"));
process.exit(data.result === expectedProject ? 0 : 1);
NODE
then
  echo "Firebase active project is not $PROJECT_ID. Run: firebase use $PROJECT_ID" >&2
  exit 2
fi

if ! firebase projects:list --json >/tmp/moonly_firebase_projects.json 2>/tmp/moonly_firebase_projects.err; then
  echo "Firebase CLI credentials could not access Firebase projects. Run: firebase login --reauth" >&2
  echo "Details: /tmp/moonly_firebase_projects.err" >&2
  exit 2
fi

npm --prefix functions run lint

echo "Deploying to project: $PROJECT_ID"
echo "Targets: $DEPLOY_ONLY"
if [[ -n "${MOONLY_DEPLOYED_SECRETS:-}" ]]; then
  echo "Bound secret names: $MOONLY_DEPLOYED_SECRETS"
else
  echo "Bound secret names: none"
fi
if [[ "${#SKIPPED_TARGETS[@]}" -gt 0 && -z "${FIREBASE_ONLY:-}" ]]; then
  echo "Skipped secret-backed functions:"
  for target in "${SKIPPED_TARGETS[@]}"; do
    echo "- $target"
  done
  echo "Set MOONLY_DEPLOYED_SECRETS=name1,name2 after creating remote secrets, or MOONLY_BIND_ALL_SECRETS=1 to bind every secret."
  echo "Set MOONLY_DEPLOY_INCOMPLETE_FUNCTIONS=1 only if you intentionally want missing-secret functions deployed as configuration-error stubs."
fi
firebase deploy --project "$PROJECT_ID" --only "$DEPLOY_ONLY"

echo
echo "Checking Functions readiness: $READINESS_URL"
READINESS_BODY="/tmp/moonly_readiness_status.json"
if curl --silent --show-error --location --max-time 30 "$READINESS_URL" >"$READINESS_BODY"; then
  node - "$READINESS_BODY" <<'NODE'
const fs = require("fs");
const path = process.argv[2];
const body = fs.readFileSync(path, "utf8");
let data;
try {
  data = JSON.parse(body);
} catch (error) {
  console.warn(`[WARN] readinessStatus returned invalid JSON: ${error.message}`);
  process.exit(0);
}

console.log(`readiness ok: ${data.ok === true ? "yes" : "no"}`);
console.log(`checkedAt: ${data.checkedAt || ""}`);
console.log(`projectId: ${data.projectId || ""}`);
console.log(`firestore: ${data.firestore && data.firestore.ok ? "ok" : "not ok"} ${data.firestore && data.firestore.message ? `(${data.firestore.message})` : ""}`);

if (data.secrets) {
  for (const [key, value] of Object.entries(data.secrets)) {
    console.log(`secret ${key}: ${value ? "set" : "missing"}`);
  }
}

if (data.secretDiagnostics && data.secretDiagnostics.note) {
  console.log(`secret diagnostics: ${data.secretDiagnostics.note}`);
  if (data.secretDiagnostics.readinessStatusCanInspectAnySecret === false) {
    console.log("secret diagnostics: readinessStatus is not currently bound to any secret.");
  }
}

if (Array.isArray(data.requiredActions) && data.requiredActions.length > 0) {
  console.log("required actions:");
  for (const action of data.requiredActions) {
    console.log(`- ${action}`);
  }
}
NODE
else
  echo "[WARN] readinessStatus check failed; run ./scripts/check-firebase-network.sh for endpoint details." >&2
fi
