#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ENV_FILE_INPUT="${RELEASE_ENV_FILE:-scripts/release.env}"
FORCE_DRY_RUN=0

usage() {
  cat <<'EOF'
Usage:
  ./scripts/finish-release.sh
  RELEASE_ENV_FILE=scripts/release.env ./scripts/finish-release.sh
  RELEASE_ENV_FILE=scripts/release.env ./scripts/finish-release.sh --dry-run
  ./scripts/finish-release.sh --env-file scripts/release.env
  ./scripts/finish-release.sh --no-env-file --dry-run

This is the final one-command release continuation.
It loads scripts/release.env by default, unless --no-env-file is used, then runs:
  - IAP Firebase secret setup
  - Firebase Functions deploy with secret bindings
  - iOS Xcode export rebuild
  - Android APK rebuild
  - release blocker gate with real sandbox receipt verification

Required local values in the env file:
  ANDROID_KEYSTORE_PASS
  ANDROID_KEYALIAS_PASS
  APPLE_SHARED_SECRET
  GOOGLE_PACKAGE_NAME
  GOOGLE_SERVICE_ACCOUNT_JSON or GOOGLE_SERVICE_ACCOUNT_JSON_FILE
  IAP_RECEIPT or REAL_IAP_RECEIPT
EOF
}

while [[ "${1:-}" != "" ]]; do
  case "$1" in
    --dry-run)
      FORCE_DRY_RUN=1
      shift
      ;;
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

if [[ -n "$ENV_FILE_INPUT" && ! -f "$ENV_FILE_INPUT" ]]; then
  cat >&2 <<EOF
Release env file does not exist: $ENV_FILE_INPUT

Create it from the template, fill the real local values, then rerun:
  cp scripts/release.env.example scripts/release.env
  RELEASE_ENV_FILE=scripts/release.env ./scripts/finish-release.sh
EOF
  exit 3
fi

cd "$ROOT_DIR"

if [[ -n "$ENV_FILE_INPUT" ]]; then
  set -a
  # shellcheck disable=SC1090
  . "$ENV_FILE_INPUT"
  set +a
  export MOONLY_RELEASE_ENV_FILE="$ENV_FILE_INPUT"
else
  unset MOONLY_RELEASE_ENV_FILE
fi
unset RELEASE_ENV_FILE

export DRY_RUN="$FORCE_DRY_RUN"
export REPORT_ONLY=0
export RUN_CONFIGURE_GOOGLE_PLAY_GAMES="${RUN_CONFIGURE_GOOGLE_PLAY_GAMES:-auto}"
export RUN_ALL_SECRET_SETUP=0
export RUN_IAP_SECRET_SETUP=1
export RUN_DEPLOY=1
export RUN_BUILDS=1
export RUN_IOS_BUILD=1
export RUN_ANDROID_BUILD=1
export RUN_RELEASE_GATE=1
export WAIT_FOR_UNITY_CLOSE=1
export CHECK_IAP_REAL_RECEIPT=1

echo "Moonly final release continuation"
if [[ -n "${MOONLY_RELEASE_ENV_FILE:-}" ]]; then
  echo "Release env file: $MOONLY_RELEASE_ENV_FILE"
else
  echo "Release env file: <none; using current environment>"
fi
if [[ "$FORCE_DRY_RUN" == "1" ]]; then
  echo "Mode: dry-run"
else
  echo "Mode: real"
fi
echo

exec "$ROOT_DIR/scripts/prepare-release.sh"
