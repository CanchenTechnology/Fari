#!/usr/bin/env bash
set -euo pipefail

PROJECT_ID="${FIREBASE_PROJECT:-fari-app-b2fd2}"

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

SECRET_SELECTION="${MOONLY_SECRET_NAMES:-${SET_SECRET_NAMES:-}}"
CUSTOM_SECRET_SELECTION=0
REQUESTED_SECRET_NAMES=()

known_secret() {
  case "$1" in
    DASHSCOPE_API_KEY|VOLC_TTS_API_KEY|VOICE_APP_ID|VOICE_ACCESS_KEY|APPLE_SHARED_SECRET|GOOGLE_PACKAGE_NAME|GOOGLE_SERVICE_ACCOUNT_JSON|PAYMENT_WEBHOOK_SECRET)
      return 0
      ;;
    *)
      return 1
      ;;
  esac
}

if [[ -n "$SECRET_SELECTION" ]]; then
  CUSTOM_SECRET_SELECTION=1
  # shellcheck disable=SC2206
  requested_names=(${SECRET_SELECTION//,/ })
  for key in "${requested_names[@]}"; do
    [[ -z "$key" ]] && continue
    if ! known_secret "$key"; then
      echo "Unknown secret name in MOONLY_SECRET_NAMES/SET_SECRET_NAMES: $key" >&2
      exit 3
    fi
    REQUESTED_SECRET_NAMES+=("$key")
  done

  if [[ "${#REQUESTED_SECRET_NAMES[@]}" -eq 0 ]]; then
    echo "MOONLY_SECRET_NAMES/SET_SECRET_NAMES did not contain any valid secret names." >&2
    exit 3
  fi
else
  REQUESTED_SECRET_NAMES=("${REQUIRED_SECRETS[@]}")
fi

if [[ -n "${MOONLY_PROXY:-}" ]]; then
  export HTTPS_PROXY="$MOONLY_PROXY"
  export HTTP_PROXY="$MOONLY_PROXY"
fi

if [[ -n "${MOONLY_ALL_PROXY:-}" ]]; then
  export ALL_PROXY="$MOONLY_ALL_PROXY"
fi

if [[ " ${REQUESTED_SECRET_NAMES[*]} " == *" GOOGLE_SERVICE_ACCOUNT_JSON "* && -z "${GOOGLE_SERVICE_ACCOUNT_JSON:-}" && -n "${GOOGLE_SERVICE_ACCOUNT_JSON_FILE:-}" ]]; then
  if [[ ! -f "$GOOGLE_SERVICE_ACCOUNT_JSON_FILE" ]]; then
    echo "GOOGLE_SERVICE_ACCOUNT_JSON_FILE does not exist: $GOOGLE_SERVICE_ACCOUNT_JSON_FILE" >&2
    exit 3
  fi

  GOOGLE_SERVICE_ACCOUNT_JSON="$(cat "$GOOGLE_SERVICE_ACCOUNT_JSON_FILE")"
fi

missing=()
for key in "${REQUESTED_SECRET_NAMES[@]}"; do
  if [[ -z "${!key:-}" ]]; then
    missing+=("$key")
  fi
done

if [[ "$CUSTOM_SECRET_SELECTION" == "0" && "${REQUIRE_PAYMENT_WEBHOOK:-0}" == "1" && -z "${PAYMENT_WEBHOOK_SECRET:-}" ]]; then
  missing+=("PAYMENT_WEBHOOK_SECRET")
fi

if [[ "${#missing[@]}" -gt 0 ]]; then
  echo "Missing required environment variable(s): ${missing[*]}" >&2
  echo "Set them in your shell, then run this script again. Values are read from env and are not written to the repo." >&2
  exit 3
fi

if [[ " ${REQUESTED_SECRET_NAMES[*]} " == *" GOOGLE_SERVICE_ACCOUNT_JSON "* && -n "${GOOGLE_SERVICE_ACCOUNT_JSON:-}" ]]; then
  if ! GOOGLE_SERVICE_ACCOUNT_JSON="$GOOGLE_SERVICE_ACCOUNT_JSON" node <<'NODE' >/dev/null 2>&1
const data = JSON.parse(process.env.GOOGLE_SERVICE_ACCOUNT_JSON || "");
if (data.type !== "service_account") throw new Error("type must be service_account");
if (!data.client_email) throw new Error("client_email is missing");
if (!data.private_key) throw new Error("private_key is missing");
NODE
  then
    echo "GOOGLE_SERVICE_ACCOUNT_JSON is not a valid Google service account JSON." >&2
    exit 3
  fi
fi

if [[ " ${REQUESTED_SECRET_NAMES[*]} " == *" GOOGLE_PACKAGE_NAME "* && -n "${GOOGLE_PACKAGE_NAME:-}" ]]; then
  if ! node - "$GOOGLE_PACKAGE_NAME" <<'NODE' >/dev/null 2>&1
const packageName = process.argv[2] || "";
const ok = /^[A-Za-z][A-Za-z0-9_]*(\.[A-Za-z][A-Za-z0-9_]*)+$/.test(packageName);
process.exit(ok ? 0 : 1);
NODE
  then
    echo "GOOGLE_PACKAGE_NAME does not look like an Android package name: $GOOGLE_PACKAGE_NAME" >&2
    exit 3
  fi
fi

if [[ "${DRY_RUN:-0}" == "1" ]]; then
  echo "Dry run OK. Requested secret environment variables are present."
  for key in "${REQUESTED_SECRET_NAMES[@]}"; do
    echo "- $key"
  done
  if [[ "$CUSTOM_SECRET_SELECTION" == "0" && -n "${PAYMENT_WEBHOOK_SECRET:-}" ]]; then
    echo "- PAYMENT_WEBHOOK_SECRET"
  elif [[ "$CUSTOM_SECRET_SELECTION" == "0" ]]; then
    echo "- PAYMENT_WEBHOOK_SECRET skipped (optional; set REQUIRE_PAYMENT_WEBHOOK=1 to require it)"
  fi
  if [[ -n "${GOOGLE_SERVICE_ACCOUNT_JSON_FILE:-}" ]]; then
    echo "GOOGLE_SERVICE_ACCOUNT_JSON source file: $GOOGLE_SERVICE_ACCOUNT_JSON_FILE"
  fi
  exit 0
fi

if ! command -v firebase >/dev/null 2>&1; then
  echo "firebase CLI not found. Install firebase-tools first." >&2
  exit 127
fi

if ! command -v node >/dev/null 2>&1; then
  echo "node not found. Node is required to validate GOOGLE_SERVICE_ACCOUNT_JSON." >&2
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

set_secret() {
  local key="$1"
  local format="${2:-string}"
  echo "Setting Firebase secret: $key"
  printf "%s" "${!key}" | firebase functions:secrets:set "$key" --project "$PROJECT_ID" --data-file - --format "$format"
}

for key in "${REQUESTED_SECRET_NAMES[@]}"; do
  if [[ "$key" == "GOOGLE_SERVICE_ACCOUNT_JSON" ]]; then
    set_secret "$key" json
  else
    set_secret "$key"
  fi
done

if [[ "$CUSTOM_SECRET_SELECTION" == "0" && -n "${PAYMENT_WEBHOOK_SECRET:-}" ]]; then
  set_secret PAYMENT_WEBHOOK_SECRET
elif [[ "$CUSTOM_SECRET_SELECTION" == "0" ]]; then
  echo "Skipping optional PAYMENT_WEBHOOK_SECRET. Set it if external payment webhooks are used."
fi

echo "Firebase secrets setup finished for project: $PROJECT_ID"
echo "Next: ./scripts/deploy-firebase.sh"
