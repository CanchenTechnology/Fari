#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TEMPLATE_FILE="$ROOT_DIR/scripts/release.env.example"
TARGET_ENV_FILE="${TARGET_ENV_FILE:-scripts/release.env}"
FORCE=0

usage() {
  cat <<'EOF'
Usage:
  ./scripts/init-release-env.sh
  ./scripts/init-release-env.sh --path scripts/release.local.env
  ./scripts/init-release-env.sh --force

Creates a local release env file from scripts/release.env.example, locks it to
owner-only permissions, and prints the next validation / release commands.

The generated env file is ignored by git. Fill it with real private values
locally; do not commit or paste secrets into logs.
EOF
}

while [[ "${1:-}" != "" ]]; do
  case "$1" in
    --path)
      if [[ -z "${2:-}" ]]; then
        echo "--path requires a file path" >&2
        exit 2
      fi
      TARGET_ENV_FILE="$2"
      shift 2
      ;;
    --force)
      FORCE=1
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

if [[ "$TARGET_ENV_FILE" != /* ]]; then
  TARGET_ENV_FILE="$ROOT_DIR/$TARGET_ENV_FILE"
fi

if [[ ! -f "$TEMPLATE_FILE" ]]; then
  echo "Release env template missing: $TEMPLATE_FILE" >&2
  exit 3
fi

if [[ -e "$TARGET_ENV_FILE" && "$FORCE" != "1" ]]; then
  display_existing="$TARGET_ENV_FILE"
  case "$display_existing" in
    "$ROOT_DIR"/*) display_existing="${display_existing#$ROOT_DIR/}" ;;
  esac

  cat >&2 <<EOF
Release env file already exists: $display_existing

Review and edit it directly, or rerun with --force to recreate it from the template.
Next validation command:
  RELEASE_ENV_FILE=$(printf '%q' "$display_existing") ./scripts/check-release-env.sh
EOF
  exit 4
fi

mkdir -p "$(dirname "$TARGET_ENV_FILE")"
cp "$TEMPLATE_FILE" "$TARGET_ENV_FILE"
chmod 600 "$TARGET_ENV_FILE"

display_path="$TARGET_ENV_FILE"
case "$display_path" in
  "$ROOT_DIR"/*) display_path="${display_path#$ROOT_DIR/}" ;;
esac
shell_path="$(printf '%q' "$display_path")"

cat <<EOF
Created release env file: $display_path
Permissions: 600

Fill the file with real local release values:
  - ANDROID_KEYSTORE_PASS
  - ANDROID_KEYALIAS_PASS
  - APPLE_SHARED_SECRET
  - GOOGLE_SERVICE_ACCOUNT_JSON_FILE or GOOGLE_SERVICE_ACCOUNT_JSON
  - IAP_RECEIPT or REAL_IAP_RECEIPT
  - RUN_PUBLIC_CONFIG_UPDATE=1 and PUBLIC_CONFIG_PATH=functions/public-config.live.json when publishing real social links / IAP display values

For public config, run ./scripts/init-public-config.sh, replace the social
homepage placeholders with real account/community URLs, and keep
REQUIRE_REAL_SOCIAL_LINKS=1 for release validation.

Then run:
  RELEASE_ENV_FILE=$shell_path ./scripts/check-release-env.sh
  RELEASE_ENV_FILE=$shell_path ./scripts/check-android-keystore.sh
  RELEASE_ENV_FILE=$shell_path REPORT_ONLY=1 ./scripts/prepare-release.sh
  RELEASE_ENV_FILE=$shell_path ./scripts/finish-release.sh
EOF
