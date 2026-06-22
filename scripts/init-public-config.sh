#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TEMPLATE_FILE="$ROOT_DIR/functions/public-config.example.json"
TARGET_CONFIG_FILE="${PUBLIC_CONFIG_PATH:-functions/public-config.live.json}"
FORCE=0

usage() {
  cat <<'EOF'
Usage:
  ./scripts/init-public-config.sh
  ./scripts/init-public-config.sh --path functions/public-config.live.json
  ./scripts/init-public-config.sh --force

Creates a local live public config file from functions/public-config.example.json.
Edit the generated file with real account/community social URLs and public IAP
display values before publishing it.
EOF
}

while [[ "${1:-}" != "" ]]; do
  case "$1" in
    --path)
      if [[ -z "${2:-}" ]]; then
        echo "--path requires a file path" >&2
        exit 2
      fi
      TARGET_CONFIG_FILE="$2"
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

if [[ "$TARGET_CONFIG_FILE" != /* ]]; then
  TARGET_CONFIG_FILE="$ROOT_DIR/$TARGET_CONFIG_FILE"
fi

if [[ ! -f "$TEMPLATE_FILE" ]]; then
  echo "Public config template missing: $TEMPLATE_FILE" >&2
  exit 3
fi

display_path="$TARGET_CONFIG_FILE"
case "$display_path" in
  "$ROOT_DIR"/*) display_path="${display_path#$ROOT_DIR/}" ;;
esac

if [[ -e "$TARGET_CONFIG_FILE" && "$FORCE" != "1" ]]; then
  cat >&2 <<EOF
Public config file already exists: $display_path

Review and edit it directly, or rerun with --force to recreate it from the template.
Next validation command:
  node functions/scripts/set-public-config.js --dry-run --require-real-social-links $(printf '%q' "$display_path")
EOF
  exit 4
fi

mkdir -p "$(dirname "$TARGET_CONFIG_FILE")"
cp "$TEMPLATE_FILE" "$TARGET_CONFIG_FILE"

shell_path="$(printf '%q' "$display_path")"

cat <<EOF
Created public config file: $display_path

Replace the social platform homepage placeholders with real account/community URLs.
Then run:
  node functions/scripts/set-public-config.js --dry-run --require-real-social-links $shell_path
  RUN_PUBLIC_CONFIG_UPDATE=1 PUBLIC_CONFIG_PATH=$shell_path REQUIRE_REAL_SOCIAL_LINKS=1 ./scripts/prepare-release.sh
EOF
