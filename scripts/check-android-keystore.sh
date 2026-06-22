#!/usr/bin/env bash
set -u

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RELEASE_ENV_FILE="${RELEASE_ENV_FILE:-}"
PROJECT_SETTINGS="$ROOT_DIR/ProjectSettings/ProjectSettings.asset"
DEFAULT_KEYTOOL="/Users/kittenhao/Unity/UnityEditor/2022.3.34f1c1/PlaybackEngines/AndroidPlayer/OpenJDK/bin/keytool"
KEYTOOL_BIN="${UNITY_ANDROID_KEYTOOL:-${KEYTOOL_BIN:-$DEFAULT_KEYTOOL}}"
FAILURES=0
WARNINGS=0
OKS=0

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

read_project_value() {
  local key="$1"
  sed -n "s/^[[:space:]]*$key:[[:space:]]*//p" "$PROJECT_SETTINGS" | head -n 1
}

resolve_keystore_path() {
  local raw="$1"
  raw="${raw%\'}"
  raw="${raw#\'}"

  case "$raw" in
    "{inproject}:"*)
      raw="${raw#"{inproject}:"}"
      raw="${raw#"${raw%%[![:space:]]*}"}"
      printf "%s/%s\n" "$ROOT_DIR" "$raw"
      ;;
    /*)
      printf "%s\n" "$raw"
      ;;
    *)
      printf "%s/%s\n" "$ROOT_DIR" "$raw"
      ;;
  esac
}

echo "Moonly Android keystore check"
echo "Project: $ROOT_DIR"
if [[ -n "$RELEASE_ENV_FILE" ]]; then
  if [[ "$RELEASE_ENV_FILE" != /* ]]; then
    RELEASE_ENV_FILE="$ROOT_DIR/$RELEASE_ENV_FILE"
  fi

  if [[ ! -f "$RELEASE_ENV_FILE" ]]; then
    fail "Release env file does not exist: $RELEASE_ENV_FILE; create it with ./scripts/init-release-env.sh"
  else
    set -a
    # shellcheck disable=SC1090
    . "$RELEASE_ENV_FILE"
    set +a
    echo "Release env file: $RELEASE_ENV_FILE"
  fi
fi
echo

if [[ "$FAILURES" -gt 0 ]]; then
  :
elif [[ ! -f "$PROJECT_SETTINGS" ]]; then
  fail "ProjectSettings/ProjectSettings.asset missing"
elif ! grep -q "androidUseCustomKeystore: 1" "$PROJECT_SETTINGS"; then
  ok "Android custom keystore is not enabled"
else
  raw_keystore="$(read_project_value "AndroidKeystoreName")"
  alias_name="$(read_project_value "AndroidKeyaliasName")"
  keystore_path="$(resolve_keystore_path "$raw_keystore")"

  if [[ -z "$raw_keystore" ]]; then
    fail "AndroidKeystoreName is missing from ProjectSettings"
  elif [[ ! -f "$keystore_path" ]]; then
    fail "Android keystore file does not exist: $keystore_path"
  else
    ok "Android keystore file exists"
  fi

  if [[ -z "$alias_name" ]]; then
    fail "AndroidKeyaliasName is missing from ProjectSettings"
  else
    ok "Android key alias is configured: $alias_name"
  fi

  if [[ -z "${ANDROID_KEYSTORE_PASS:-}" ]]; then
    fail "ANDROID_KEYSTORE_PASS is missing"
  else
    ok "ANDROID_KEYSTORE_PASS is present"
  fi

  if [[ -z "${ANDROID_KEYALIAS_PASS:-}" ]]; then
    fail "ANDROID_KEYALIAS_PASS is missing"
  else
    ok "ANDROID_KEYALIAS_PASS is present"
  fi

  if [[ ! -x "$KEYTOOL_BIN" ]]; then
    if command -v keytool >/dev/null 2>&1; then
      KEYTOOL_BIN="$(command -v keytool)"
      warn "Unity Android keytool not found; using system keytool"
    else
      fail "keytool not found"
    fi
  else
    ok "keytool is available"
  fi

  if [[ "$FAILURES" -eq 0 ]]; then
    list_log="/tmp/moonly_android_keystore_list.log"
    if "$KEYTOOL_BIN" \
      -list \
      -keystore "$keystore_path" \
      -storepass "$ANDROID_KEYSTORE_PASS" \
      -alias "$alias_name" \
      >/tmp/moonly_android_keystore_list.out 2>"$list_log"; then
      ok "Android keystore password opens the keystore and alias exists"
    else
      fail "Android keystore password or alias is invalid; see $list_log"
    fi
  fi

  if [[ "$FAILURES" -eq 0 ]]; then
    if truthy "${SKIP_ANDROID_KEYALIAS_PASS_CHECK:-0}"; then
      warn "Android key alias password validation skipped"
    else
      import_log="/tmp/moonly_android_keystore_keypass.log"
      tmp_keystore="$(mktemp "/tmp/moonly-android-keypass-check.XXXXXX")"
      rm -f "$tmp_keystore"
      trap 'rm -f "$tmp_keystore"' EXIT

      if "$KEYTOOL_BIN" \
        -importkeystore \
        -srckeystore "$keystore_path" \
        -srcstorepass "$ANDROID_KEYSTORE_PASS" \
        -srcalias "$alias_name" \
        -srckeypass "$ANDROID_KEYALIAS_PASS" \
        -destkeystore "$tmp_keystore" \
        -deststorepass "MoonlyReleaseCheck123!" \
        -destkeypass "MoonlyReleaseCheck123!" \
        -noprompt \
        >/tmp/moonly_android_keystore_keypass.out 2>"$import_log"; then
        ok "Android key alias password can read the signing key"
      else
        fail "Android key alias password is invalid; see $import_log"
      fi
    fi
  fi
fi

echo
echo "Summary: $FAILURES failure(s), $WARNINGS warning(s), $OKS ok check(s)"
if [[ "$FAILURES" -gt 0 ]]; then
  exit 1
fi

exit 0
